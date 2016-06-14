using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System;
using System.Text;

internal static class SphinxPlugin
{
	#if UNITY_STANDALONE || UNITY_EDITOR
	const string dll = "unitysphinx";
	#endif

	[DllImport (dll, EntryPoint = "Recognize_Mic")]
	public static extern int Recognize_Mic();

	[DllImport (dll, EntryPoint = "Init_Recognizer")]
	public static extern int Init_Recognizer(int audio, int search,
		StringBuilder hmm, StringBuilder lm, StringBuilder dict,
		StringBuilder jsgf, StringBuilder kws);

	[DllImport (dll, EntryPoint = "Recognizer_Enabled")]
	public static extern int Recognizer_Enabled();

	[DllImport (dll, EntryPoint = "Is_utt_started")]
	public static extern int Is_utt_started();

	[DllImport (dll, EntryPoint = "Is_ad_Enabled")]
	public static extern int Is_ad_Enabled();

	[DllImport (dll, EntryPoint = "Is_ps_Enabled")]
	public static extern int Is_ps_Enabled();

	[DllImport (dll, EntryPoint = "Is_config_Enabled")]
	public static extern int Is_config_Enabled();

	[DllImport (dll, EntryPoint = "Get_Search_Model")]
	public static extern int Get_Search_Model(StringBuilder str, int strlen);

	[DllImport (dll, EntryPoint = "Set_Search_Model")]
	public static extern int Set_Search_Model(int search);

	[DllImport (dll, EntryPoint = "Set_kws")]
	public static extern int Set_kws(StringBuilder str);

	[DllImport (dll, EntryPoint = "Set_jsgf")]
	public static extern int Set_jsgf(StringBuilder str);

	[DllImport (dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Dequeue_String")]
	public static extern int Dequeue_String(StringBuilder str, int strlen);

	[DllImport (dll, EntryPoint = "Get_Queue_Length")]
	public static extern int Get_Queue_Length();

	[DllImport (dll, EntryPoint = "Request_Buffer_Length")]
	public static extern int Request_Buffer_Length();

	[DllImport (dll, EntryPoint = "Free_Recognizer")]
	public static extern int Free_Recognizer();

	[DllImport (dll, EntryPoint = "Is_Recognizer_Enabled")]
	public static extern int Is_Recognizer_Enabled();
}