using UnityEngine;
using System.Collections;
using System;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;
public class ClearSceneData : MonoBehaviour {

	//异步操作对象
	private AsyncOperation async;

	private static int nextScene;


	void Start()
	{
		StartCoroutine("ClearResouces");
	}

	IEnumerator ClearResouces(){
		Resources.UnloadUnusedAssets();
		yield return new WaitForSeconds (0.1f);
			
		//卸载没有被引用的资源
		Resources.UnloadUnusedAssets();

		//立即进行垃圾回收
		GC.Collect();
		GC.WaitForPendingFinalizers();//挂起当前线程，直到处理终结器队列的线程清空该队列为止
		GC.Collect();


		yield return null;
		StartCoroutine("AsyncLoadScene", nextScene);
	}

	/// <summary>
	/// 静态方法，直接切换到ClearScene，此脚本是挂在ClearScene场景下的，就会实例化，执行资源回收
	/// </summary>
	/// <param name="_nextSceneName"></param>
	public static void LoadScene(int _nextScene)
	{
		nextScene = _nextScene;
		SceneManager.LoadScene (GameConfig.clearScene);  

	}

	/// <summary>
	/// 异步加载下一个场景
	/// </summary>
	/// <param name="sceneName"></param>
	/// <returns></returns>
	IEnumerator AsyncLoadScene(int scene)
	{
		async = SceneManager.LoadSceneAsync(scene);
		yield return async;
	}

	void OnDestroy()
	{
		async = null;
//		pictureAB.Unload (true);
		Resources.UnloadUnusedAssets();
	}
}
