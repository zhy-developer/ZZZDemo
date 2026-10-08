// Standalone tests only. These adapters do NOT prove Unity serialization or Editor behavior.
using System.Text.Json;
namespace UnityEngine {
 public class Object { public string name; }
 public class ScriptableObject : Object { }
 public class AnimationClip : Object { public float length; public float frameRate; }
 public class RuntimeAnimatorController : Object { }
 public class GameObject : Object { }
 public class AudioClip : Object { }
 public class TextAsset : Object { public string text; public TextAsset(string value) { text = value; } }
 public class SerializeField : Attribute { }
 public class SerializeReference : Attribute { }
 public class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; }
 public static class Resources { public static T Load<T>(string path) where T : Object => null; }
 public static class JsonUtility {
   static JsonSerializerOptions Options(bool pretty = false) => new() { IncludeFields = true, WriteIndented = pretty };
   public static string ToJson(object o, bool pretty = false) => JsonSerializer.Serialize(o, o.GetType(), Options(pretty));
   public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options());
 }
}
public class ComboData : UnityEngine.ScriptableObject { public SkillConfig.SkillAuthoringData skill; public float comboDamage; public string comboName; }
