using System.Text.Json;

// Compare independently recorded consumed inputs FIRST, then named authority fields.
// No interpretation of particle/render state; no assumption that equal terminal hashes suffice.
static class TraceChecks
{
    sealed record Row(string Match,int Round,long Frame,string Stage,string Rules,string Config,int Seed,string Input,string Hash,SortedDictionary<string,string> Fields);
    static Row Read(string line) {
        using var d=JsonDocument.Parse(line);var r=d.RootElement;
        var fields=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(var f in r.GetProperty("fields").EnumerateArray()) if(!fields.TryAdd(f.GetProperty("name").GetString(),f.GetProperty("value").GetString()))throw new Exception("Duplicate field");
        return new(r.GetProperty("match").GetString(),r.GetProperty("round").GetInt32(),r.GetProperty("frame").GetInt64(),r.GetProperty("stage").GetString(),r.GetProperty("rules").GetString(),r.GetProperty("config").GetString(),r.GetProperty("seed").GetInt32(),r.GetProperty("input").GetString(),r.GetProperty("hash").GetString(),fields);
    }
    static object Difference(Row a,Row b) {
        if(a.Match!=b.Match||a.Round!=b.Round||a.Frame!=b.Frame||a.Stage!=b.Stage)return new{kind="scope/stage",a,b};
        if(a.Rules!=b.Rules||a.Config!=b.Config||a.Seed!=b.Seed)return new{kind="initial/config",a,b};
        if(a.Input!=b.Input)return new{kind="consumed-input",frame=a.Frame,first=a.Input,second=b.Input};
        var changes=a.Fields.Keys.Union(b.Fields.Keys,StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal)
            .Where(k=>a.Fields.GetValueOrDefault(k)!=b.Fields.GetValueOrDefault(k)).Select(k=>new{field=k,first=a.Fields.GetValueOrDefault(k),second=b.Fields.GetValueOrDefault(k)}).ToArray();
        if(changes.Length>0)return new{kind="authority-field",frame=a.Frame,changes};
        if(a.Hash!=b.Hash)return new{kind="hash-without-fields",frame=a.Frame,first=a.Hash,second=b.Hash};
        return null;
    }
    static int Compare(string first,string second,string output) {
        var paths=Directory.GetFiles(first,"*.jsonl").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        if(paths.Length==0||paths.Length>24||Directory.GetFiles(second,"*.jsonl").Length!=paths.Length)throw new Exception("Missing match or capacity violation");
        long count=0;var history=new Queue<object>();
        foreach(var path in paths) {
            var other=Path.Combine(second,Path.GetFileName(path));if(!File.Exists(other))throw new Exception("Peer missing match "+path);
            using var aa=File.OpenText(path);using var bb=File.OpenText(other);long previous=0;int frames=0;
            while(true) {
                string al=aa.ReadLine(),bl=bb.ReadLine();if(al==null&&bl==null)break;
                object diff;Row a=null,b=null;
                if(al==null||bl==null)diff=new{kind="missing-frame",first=al,second=bl};
                else {a=Read(al);b=Read(bl);if(a.Frame!=previous+1||++frames>10000)throw new Exception("Noncontinuous/duplicate frame or capacity exceeded");previous=a.Frame;diff=Difference(a,b);}
                if(diff!=null) {File.WriteAllText(output,JsonSerializer.Serialize(new{file=Path.GetFileName(path),difference=diff,context=history.ToArray(),first=a,second=b},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("TRACE_FAIL "+output);return 1;}
                count++;history.Enqueue(new{a,b});if(history.Count>4)history.Dequeue();
            }
            if(frames==0)throw new Exception("Empty match");history.Clear();
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{status="PASS",matches=paths.Length,frames=count,comparison="consumed input then named authority fields + canonical SHA256",limitations="hash-only runs detect divergence but require detailed rerun for field localization"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("TRACE_PASS matches="+paths.Length+" frames="+count);return 0;
    }
    public static int Main(string[] args) {
        try {
            if(args.Length==4&&args[0]=="compare")return Compare(args[1],args[2],args[3]);
            var fields=new SortedDictionary<string,string>(StringComparer.Ordinal){{"hp","100"},{"motion.cursor","1"}};
            var a=new Row("match",1,1,"post-commit","v1","config",7,"noop","hash",fields);int n=0;
            void Check(bool yes,string label){if(!yes)throw new Exception(label);Console.WriteLine("TRACE_CHECK_PASS "+label);n++;}
            Check(Difference(a,a)==null,"identical snapshots");
            Check(JsonSerializer.Serialize(Difference(a,a with{Input="attack",Fields=new(){{"hp","0"}}})).Contains("consumed-input"),"input compared before state");
            Check(JsonSerializer.Serialize(Difference(a,a with{Fields=new(){{"hp","100"},{"motion.cursor","2"}}})).Contains("motion.cursor"),"same legacy hash still detects missing motion state");
            Check(Difference(a,a with{Frame=2})!=null,"frame stage alignment");
            Check(Difference(a,a with{Seed=8})!=null,"seed alignment");
            Check(Difference(a,a with{Fields=new(){{"motion.cursor","1"},{"hp","100"}}})==null,"field enumeration independent");
            Check(Difference(a,a with{Hash="different"})!=null,"hash-only divergence");
            string temporary=Path.Combine(Path.GetTempPath(),"phase5-trace-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try {
                string left=Path.Combine(temporary,"A"),right=Path.Combine(temporary,"B"),result=Path.Combine(temporary,"result.json");
                Directory.CreateDirectory(left);Directory.CreateDirectory(right);
                string Serialize(Row r)=>JsonSerializer.Serialize(new{match=r.Match,round=r.Round,frame=r.Frame,stage=r.Stage,rules=r.Rules,config=r.Config,seed=r.Seed,input=r.Input,hash=r.Hash,fields=r.Fields.Select(f=>new{name=f.Key,value=f.Value})});
                void Write(string directory,params Row[] rows)=>File.WriteAllLines(Path.Combine(directory,"match.jsonl"),rows.Select(Serialize));
                bool Rejects(Action action){try{action();return false;}catch(Exception){return true;}}
                Write(left,a,a with{Frame=2});Write(right,a,a with{Frame=2});
                Check(Compare(left,right,result)==0,"actual JSONL files compare continuously");
                Write(right,a,a with{Frame=2,Input="attack",Fields=new(){{"hp","0"}}});
                Check(Compare(left,right,result)==1 && File.ReadAllText(result).Contains("consumed-input") && File.ReadAllText(result).Contains("context"),"first file divergence preserves input and prior context");
                Write(right,a);Check(Compare(left,right,result)==1 && File.ReadAllText(result).Contains("missing-frame"),"missing peer tail detected");
                Write(left,a,a);Write(right,a,a);Check(Rejects(()=>Compare(left,right,result)),"duplicate frame rejected even when both files agree");
                Write(left,a with{Frame=2});Write(right,a with{Frame=2});Check(Rejects(()=>Compare(left,right,result)),"missing initial frame rejected");
                Write(left);Write(right);Check(Rejects(()=>Compare(left,right,result)),"empty traces cannot pass");
                File.Delete(Path.Combine(right,"match.jsonl"));Check(Rejects(()=>Compare(left,right,result)),"missing match cannot pass");
            } finally {Directory.Delete(temporary,true);}
            Console.WriteLine("TRACE_CHECK_COMPLETE checks="+n+" standalone assertions");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
