using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

//using System.Threading;
using System.IO;

public class StreamingAssetService : Singleton<StreamingAssetService>
{
	//StreamingAssets文件夹路径
	public string m_sStreamingAssetsPath;
	//assetbundle对应的平台后缀
	public string m_strABExtra;
	private ClassForUpdate classForUpdate;

	public StreamingAssetService ()
	{
		GameObject obj = new GameObject ("GlobalObj");
		classForUpdate = obj.AddComponent<ClassForUpdate> ();
		GameObject.DontDestroyOnLoad (obj);

		#if UNITY_ANDROID  && !UNITY_EDITOR
		m_sStreamingAssetsPath = "jar:file://" + Application.dataPath + "!/assets";   
		#elif UNITY_IPHONE && !UNITY_EDITOR
		m_sStreamingAssetsPath = Application.streamingAssetsPath;
		#else  
		m_sStreamingAssetsPath = Application.streamingAssetsPath;
		#endif 

		#if UNITY_ANDROID 
		m_strABExtra = "_android";   
		#elif UNITY_IOS 
		m_strABExtra = "_ios";  
		#elif UNITY_STANDALONE_OSX 
		m_strABExtra = "_mac"; 
		#elif UNITY_STANDALONE_WIN 
		m_strABExtra = "_win"; 
		#else  
		Debug.Log("没有ab文件的平台啊～～～～");
		#endif 
	}

	public void SetScreenResolution (int _width, int _heigh)
	{
//		#if (UNITY_ANDROID || UNITY_IPHONE) && !UNITY_EDITOR
//		Screen.SetResolution (_width,_heigh,true);
//		scaleScreen =  originScreenSize.y / _heigh;
//		#endif  
	}

	public bool IsChinese ()
	{
		return Application.systemLanguage == SystemLanguage.Chinese;
	}

	public string GetABPath (string _file)
	{
		return m_sStreamingAssetsPath + "/AssetBundle/" + _file + m_strABExtra;
	}

	//读取StreamingAssets下的文件
	public void GetFileStringFromStreamingAssets (string _fileName, Action<string> _action)
	{
		string fullPath = m_sStreamingAssetsPath + "/" + _fileName;
		classForUpdate.GetFileStringFromStreamingAssets (fullPath, _action);
	}
 
}

public class ClassForUpdate : MonoBehaviour
{
	public void GetFileStringFromStreamingAssets (string _fileName, Action<string> _action)
	{
		#if UNITY_ANDROID  && !UNITY_EDITOR
		StartCoroutine (GetFileStringForAndroid(_fileName,_action));
		#elif UNITY_IPHONE && !UNITY_EDITOR
		GetFileStringForIos(_fileName,_action);
		#else  
		GetFileString (_fileName, _action);
		#endif 
	}

	IEnumerator GetFileStringForAndroid (string _path, Action<string> _action)
	{ 
		WWW wwwCar = new WWW (_path);  
		yield return wwwCar;  
		_action (wwwCar.text);
	}

	private void GetFileStringForIos (string _path, Action<string> _action)
	{  
		if (File.Exists (_path)) { 
			try {  
				StreamReader sr = File.OpenText (_path); 
				_action (sr.ReadToEnd ());
				sr.Close (); 
				sr.Dispose ();
			} catch {
				Debug.Log ("_path_car出错咯～");
			} 
		} 
	}

	private void GetFileString (string _path, Action<string> _action)
	{
		string text;
		try {
			// Multiple clients may read the same installation's immutable tables.
			using (var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (var reader = new StreamReader(file)) {
				text = reader.ReadToEnd();
			}
		} catch (Exception ex) {
			// Do not allow initialization to continue with an empty direction table.
			throw new IOException("Failed to read StreamingAsset: " + _path, ex);
		}

		// Release the file before invoking a parser, including when parsing throws.
		try {
			_action?.Invoke(text);
		} catch (Exception ex) {
			throw new InvalidDataException("Failed to process StreamingAsset: " + _path, ex);
		}
	}
}
