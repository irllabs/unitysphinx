using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;

public class UnitySphinx : MonoBehaviour {
	static UnitySphinx instance;
	static float lastRecognized;
	static bool running;
	static bool initialized;

	public enum SearchModel
	{
		jsgf,
		kws
	}
	public enum AudioDevice
	{
		File,
		Mic
	}

	static SearchModel searchModel = SearchModel.kws;
	static AudioDevice audioDevice = AudioDevice.Mic;
	static string hmm;
	static string lm;
	static string dict;
	static string jsgf;
	static string kws;

	void Awake()
	{
		if (instance != null) {
			Debug.LogError ("There is more than one instance of UnitySphinx in this scene.");
		}
		instance = this;
	}

	public static void Run()
	{
		if (!initialized || SphinxPlugin.Is_Recognizer_Enabled() == 0) {
			Debug.LogWarning ("Attempting to run a recognizer that has not been initialized.");
			return;
		}
		running = true;
	}

	public static void Pause()
	{
		if (!initialized || SphinxPlugin.Is_Recognizer_Enabled() == 0) {
			Debug.LogWarning ("Attempting to pause a recognizer that has not been initialized.");
			return;
		}
		running = false;
	}

	// Use this for initialization
	public static void Init() {
		// a good efficiency trick would be to avoid using Resources in the build
		// maybe allow the developer to set a list of different jsgf/kws resources
		hmm = Application.dataPath + "/Resources/UnitySphinx/model/en-us/en-us";
		lm = Application.dataPath + "/Resources/UnitySphinx/model/en-us/en-us.lm.bin";
		dict = Application.dataPath + "/Resources/UnitySphinx/model/en-us/cmudict-en-us.dict";
		jsgf = Application.dataPath + "/Resources/UnitySphinx/model/en-us/animals.gram";
		kws = Application.dataPath + "/Resources/UnitySphinx/model/en-us/keyphrase.list";
		Init (audioDevice, searchModel, hmm, lm, dict, jsgf, kws);
	}

	public static void Init (AudioDevice audioDevice, SearchModel searchModel, string hmm, string lm, string dict, string jsgf, string kws)
	{
		int searchInt = SearchModelToSearchInt(searchModel);
		int audioInt = 0;

		if (audioDevice == AudioDevice.Mic) {
			audioInt = 0;
		} else if (audioDevice == AudioDevice.File) {
			audioInt = 1;
		}

		StringBuilder hmmSB = new StringBuilder (hmm.Length);
		StringBuilder lmSB = new StringBuilder (lm.Length);
		StringBuilder dictSB = new StringBuilder (dict.Length);
		StringBuilder jsgfSB = new StringBuilder (jsgf.Length);
		StringBuilder kwsSB = new StringBuilder (kws.Length);

		hmmSB.Append (hmm);
		lmSB.Append (lm);
		dictSB.Append (dict);
		jsgfSB.Append (jsgf);
		kwsSB.Append (kws);

		if (instance == null) {
			//By Unity's design, an instance is required to start a coroutine for speech recognition
			Debug.LogError ("There needs to be one instance of UnitySphinx in the scene. Initialization aborted.");
			return;
		}

		if (initialized || running || SphinxPlugin.Is_Recognizer_Enabled() == 1) {
			Debug.LogError ("There was an attempt to initialize a new instance of UnitySphinx without stopping it. Aborted.");
			return;
		}
		//Stop ();

		int errcode = SphinxPlugin.Init_Recognizer (audioInt, searchInt,
			hmmSB, lmSB, dictSB, jsgfSB, kwsSB);
		if (errcode != 0) 
		{
			Debug.LogError("Pocketsphinx recognizer object failed to initialize.");
			if (errcode == -20)
				Debug.LogError ("Config failed to initialize properly.");
			else if (errcode == -21)
				Debug.LogError ("Check that all your dictionary, grammar, and acoustic model paths are correct.");
			else if (errcode == -31)
				Debug.LogError ("Failed to open mic device.");
			else if (errcode == -32)
				Debug.LogError ("Failed to start recording through mic.");
			else if (errcode == -33)
				Debug.LogError ("Failed to start utterance.");
			else if (errcode == -61)
				Debug.LogWarning ("Pocketsphinx recognizer object was not initialized properly. Failed to set kws file.");
			else if (errcode == -62)
				Debug.LogWarning ("Failed to set kws file. Ensure the path is valid.");
			else if (errcode == -71)
				Debug.LogWarning ("Pocketsphinx recognizer object was not initialized properly. Failed to set jsgf file.");
			else if (errcode == -72)
				Debug.LogWarning ("Failed to set jsgf file. Ensure the path is valid.");
			else
				Debug.LogWarning ("Some other problem happened");
			return;
		}
		initialized = true;
		instance.StartCoroutine ("Recognize");
	}

	public static void Stop()
	{
		if (!initialized || SphinxPlugin.Is_Recognizer_Enabled() == 0) {
			Debug.LogWarning ("Attempting to stop a recognizer that has not been initialized.");
		}
		SphinxPlugin.Free_Recognizer ();
		instance.StopCoroutine ("Recognize");
		initialized = false;
		running = false;
	}

	IEnumerator Recognize ()
	{
		while (true) {
			yield return null;
			if (running && Time.time - lastRecognized >= 0.1f) 
			{
				lastRecognized = Time.time;
				SphinxPlugin.Recognize_Mic ();
			}
		}
	}

	public static string DequeueString ()
	{
		int strlen = SphinxPlugin.Request_Buffer_Length ();
		// Not sure what the buffer size difference is between
		// char * and StringBuilder, so just add 10 more bytes
		strlen += 10;
		StringBuilder str = new StringBuilder (strlen);
		if (strlen > 0) {
			SphinxPlugin.Dequeue_String (str, strlen);
		}

		return str.ToString ();
	}

	public static int GetQueueLength ()
	{
		return SphinxPlugin.Get_Queue_Length();
	}

	public static bool IsInitialized()
	{
		if ((initialized && SphinxPlugin.Is_Recognizer_Enabled () == 0) ||
			(!initialized && SphinxPlugin.Is_Recognizer_Enabled () == 1)) {
			Debug.LogError ("DLL and UnitySphinx somehow got out of sync. This should be impossible.");
			return false;
		}
		bool isInitialized = initialized && SphinxPlugin.Is_Recognizer_Enabled () == 1;
		return isInitialized;
	}

	public static bool IsRunning()
	{
		if ((running && SphinxPlugin.Is_Recognizer_Enabled () == 0) ||
		    (!running && SphinxPlugin.Is_Recognizer_Enabled () == 1)) {
			Debug.LogError ("DLL and UnitySphinx somehow got out of sync. This should be impossible.");
			return false;
		}
		bool isRunning = running && SphinxPlugin.Is_Recognizer_Enabled () == 1;
		return isRunning;
	}


	public static bool IsUtteranceStarted()
	{
		return SphinxPlugin.Is_utt_started () == 1;
	}

	public static string GetSearchModel()
	{
		StringBuilder str = new StringBuilder (10);
		int strlen = str.Capacity;
		int errcode = SphinxPlugin.Get_Search_Model (str, strlen);
		if (errcode == -91) {
			Debug.LogWarning ("Pocketsphinx recognizer object was not initialized properly.");
		}
		else if (errcode == -92) {
			Debug.LogWarning ("Pocketsphinx has no search model enabled.");
		}
		return str.ToString ();
	}

	public static void SetSearchModel(SearchModel newModel)
	{
		Stop();
		/*
		int searchInt = SearchModelToSearchInt (searchModel);
		int errcode = SphinxPlugin.Set_Search_Model (searchInt);
		if (errcode == -60)
			Debug.LogWarning ("Pocketsphinx failed to reinitialize while setting kws as search mode.");
		else if (errcode == -61)
			Debug.LogWarning("Pocketsphinx recognizer object was not initialized properly. Failed to set kws file.");
		else if (errcode == -62)
			Debug.LogWarning ("Failed to set kws file. Ensure the path is valid.");
		else if (errcode == -70)
			Debug.LogWarning ("Pocketsphinx failed to reinitialize while setting jsgf as search mode.");
		else if (errcode == -71)
			Debug.LogWarning("Pocketsphinx recognizer object was not initialized properly. Failed to set jsgf file.");
		else if (errcode == -72)
			Debug.LogWarning ("Failed to set jsgf file. Ensure the path is valid.");
		else if (errcode == -81)
			Debug.LogWarning("Pocketsphinx recognizer object was not initialized properly.");
		else if (errcode == -82)
			Debug.LogWarning ("The jsgf file was not configured properly.");
		else if (errcode == -83)
			Debug.LogWarning ("The kws file was not configured properly.");
			*/
		searchModel = newModel;
		Init ();
		Run ();
	}

	public static void SetjsgfPath(string path)
	{
		Stop();
		StringBuilder str = new StringBuilder (path.Length);
		str.Append (path);
		int errcode = SphinxPlugin.Set_jsgf (str);
		if (errcode == -71)
			Debug.LogWarning("Pocketsphinx recognizer object was not initialized properly. Failed to set kws file.");
		else if (errcode == -72)
			Debug.LogWarning ("Failed to set jsgf file. Ensure the path is valid.");
		instance.StartCoroutine ("Recognize");
		searchModel = SearchModel.jsgf;
		Init ();
		Run ();
	}

	public static void SetkwsPath(string path)
	{
		Stop ();
		StringBuilder str = new StringBuilder (path.Length);
		str.Append (path);
		int errcode = SphinxPlugin.Set_kws(str);
		if (errcode == -61)
			Debug.LogWarning("Pocketsphinx recognizer object was not initialized properly. Failed to set kws file.");
		else if (errcode == -62)
			Debug.LogWarning ("Failed to set kws file. Ensure the path is valid.");
		instance.StartCoroutine ("Recognize");
		searchModel = SearchModel.kws;
		Init ();
		Run ();
	}

	private static int SearchModelToSearchInt(SearchModel searchModel)
	{
		if (searchModel == SearchModel.jsgf) {
			return 0;
		}
		else if (searchModel == SearchModel.kws) {
			return 1;
		}
		return -1;
	}

	void OnApplicationQuit()
	{
		if (initialized || SphinxPlugin.Is_Recognizer_Enabled () == 1)
			Stop ();
	}
}