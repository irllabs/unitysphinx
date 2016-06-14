//////////////////////////////////////////////////////////////////////////////////////
//                                                                                  //
//                                                                                  //
//                                                                                  //
//                                                                                  //
//                                                                                  //
//                                                                                  //
//                                                                                  //
//                                                                                  //
//                                                                                  //
//                                                                                  //
//////////////////////////////////////////////////////////////////////////////////////

// To do: Allow recognition from audio file
// preload all jsgf and kws files during build instead of loading from Resources
// allow other language models besides jsgf and kws

#include <stdio.h>
#include <string.h>
#include <assert.h>

#if defined(_WIN32) && !defined(__CYGWIN__)
#include <windows.h>
#else
#include <sys/select.h>
#endif

#include <sphinxbase/err.h>
#include <sphinxbase/ad.h>
#include <sphinxbase/jsgf.h>

#define DllExport __declspec (dllexport)

extern "C" {
	#include "pocketsphinx.h"

	struct StringNode {
		char *str;
		StringNode *next;
	};

	static StringNode *header = new StringNode(); //header for the queue of recognized strings
	static int queueLength = 0;
	static ps_decoder_t *ps = NULL; //pocketsphinx recognizer object
	static cmd_ln_t *config = NULL; //container for recognizer settings
	static FILE *rawfd; //
	static ad_rec_t *ad = NULL; //audio device
	static uint8 utt_started; //utterance is started (silence is broken)
	int16 adbuf[2048];
	uint8 in_speech;
	int32 k;
	char const *hyp; //recognized string hypothesis
	char *pJsgf;
	char *pKws;

	static const arg_t cont_args_def[] = {
		POCKETSPHINX_OPTIONS,
		/* Argument file. */
		{ "-argfile",
		ARG_STRING,
		NULL,
		"Argument file giving extra arguments." },
		{ "-adcdev",
		ARG_STRING,
		NULL,
		"Name of audio device to use for input." },
		{ "-infile",
		ARG_STRING,
		NULL,
		"Audio file to transcribe." },
		{ "-inmic",
		ARG_BOOLEAN,
		"yes",
		"Transcribe audio from microphone." },
		{ "-time",
		ARG_BOOLEAN,
		"no",
		"Print word times in file transcription." },
		CMDLN_EMPTY_OPTION
	};

	//debugging features
	DllExport int Is_utt_started()
	{
		return utt_started;
	}

	DllExport int Is_ad_Enabled()
	{
		return ad != NULL;
	}

	DllExport int Is_ps_Enabled()
	{
		return ps != NULL;
	}

	DllExport int Is_config_Enabled()
	{
		return config != NULL;
	}

	DllExport int Is_Recognizer_Enabled()
	{
		if (Is_ad_Enabled() && Is_ps_Enabled() && Is_config_Enabled())
			return 1;
		else
			return 0;
	}

	DllExport int Get_Search_Model(char *str, int strlen)
	{
		if (!Is_ps_Enabled())
		{
			//pocketsphinx was not initialized properly
			return -91;
		}
		if (ps_get_search(ps) == NULL)
		{
			//pocketsphinx has no search mode enabled
			return -92;
		}
		strcpy_s(str, strlen, ps_get_search(ps));
		return 0;
	}

	DllExport int Get_Queue_Length()
	{
		return queueLength;
	}

	DllExport int Set_jsgf(const char *jsgf)
	{
		if (!Is_ps_Enabled())
			//pocketsphinx was not initialized properly
			return -71;
		if (ps_reinit(ps, config) != 0)
			return -70;
		if (ps_set_jsgf_file(ps, "jsgf", jsgf) != 0)
			//the jsfg file was not configured properly
			return -72;
		if (ps_set_search(ps, "jsgf") != 0)
			//the jsfg file was not configured properly
			return -82;
		return 0;
	}

	DllExport int Set_kws(const char *kws)
	{
		if (!Is_ps_Enabled())
			//pocketsphinx was not initialized properly
			return -61;
		if (ps_reinit(ps, config) != 0)
			return -60;
		if (ps_set_kws(ps, "kws", kws) != 0)
			//the kws file was not configured properly
			return -62;
		if (ps_set_search(ps, "kws") != 0)
			//the kws file was not configured properly
			return -83;
		return 0;
	}

	DllExport int Set_Search_Model(int search)
	{	
		int errcode = 0;
		if (search == 0)
		{
			errcode = Set_jsgf(pJsgf);
		}
		else if (search == 1)
		{
			errcode = Set_kws(pKws);
		}
		return errcode;
	}

	//recognizer needs to be manually freed after use
	DllExport int Free_Recognizer()
	{
		//free all strings in the queue except for the header
		while (header->next != NULL)
		{
			StringNode *tmpNode = header->next->next;
			free(header->next);
			header->next = tmpNode;
		}
		queueLength = 0;
		if (Is_ps_Enabled())
		{
			ps_free(ps);
			ps = NULL;
		}
		if (Is_config_Enabled())
		{
			cmd_ln_free_r(config);
			config = NULL;
		}
		if (Is_ad_Enabled())
		{
			ad_close(ad);
			ad = NULL;
		}
		return 0;
	}

	//////////////////////////////////////////////////////////////////
	//																//
	//     The next two functions, Request_Buffer_Length and        // 
	//     Dequeue_String, MUST be used in conjunction very			//
	//     carefully, due to limitations of overcoming the			//
	//     interop boundary. If you want to use or modify these,	//
	//     you are welcome to do so, as this is unlikely to be		//
	//     the most elegant solution, but do so only if you			//
	//     understand exactly what you are doing.					//
	//																//
	//////////////////////////////////////////////////////////////////

	//Get the length of the next string in the queue.
	//This MUST be called before every call to Dequeue_String to let the Unity-side 
	//wrapper know how large a buffer it needs to allocate to receive the next string
	DllExport int Request_Buffer_Length()
	{
		if (queueLength == 0)
			return 0;
		if (header->next == NULL)
			E_FATAL("queueLength somehow got out of sync. This should be impossible.");
		return strlen(header->next->str);
	}

	//Get the next string in the queue. It's safe to call even if the queue is empty.
	//Every call to this function must be called after strlen was determined with 
	//a new call to Request_Buffer_Length
	DllExport int Dequeue_String(char *str, int strlen)
	{
		if (strlen == 0)
			return -51;
		if (queueLength == 0)
			return -52;

		const char* tmpString = header->next->str;
		strcpy_s(str, strlen, tmpString);

		StringNode *tmpNode = header->next->next;
		free(header->next);
		header->next = tmpNode;
		queueLength--;
		if (queueLength < 0)
			E_FATAL("queueLength somehow got out of sync. This should be impossible.");
		return 0;
	}

	int Enqueue_String(char *newString)
	{
		StringNode *newNode = new StringNode();
		newNode->str = newString;
		newNode->next = header->next;
		header->next = newNode;
		queueLength++;
		return 0;
	}

	//initialize the pocketsphinx decoder. this is hidden from the interface
	//because it should always be called through Init_Recognizer
	int Init_Decoder(int audio, int search, const char *hmm, const char *lm, const char *dict,
		const char *jsgf, const char *kws)
	{
		//nasty typecasting, but necessary for Unity and C++ to talk to each other
		char *Hmm = _strdup(hmm);
		char *Lm = _strdup(lm);
		char *Dict = _strdup(dict);
		char *Jsgf = _strdup(jsgf);
		char *Kws = _strdup(kws);

		pJsgf = Jsgf;
		pKws = Kws;

		char *argv[] = { "unitysphinx.dll","-inmic","yes","-hmm",
			Hmm,"-lm",Lm,"-dict",Dict,NULL };
		int argc = sizeof(argv) / sizeof(char*) - 1;
		//initialize config
		//remember to include a way of passing grammar files
		config = cmd_ln_parse_r(NULL, cont_args_def, argc, argv, TRUE);
		cmd_ln_set_str_r(config, "-jsgf", Jsgf);
		cmd_ln_set_str_r(config, "-kws", Kws);
				//find grammar files passed through config
		ps_default_search_args(config);
		if (config == NULL)
			//config failed to initialize
			return -20;
		ps = ps_init(config);
		//initializing recognizer failed
		if (!Is_ps_Enabled())
			//Ensure that all your dictionary and grammar paths are correct.
			return -21;
		int errcode = 0;
		errcode = Set_Search_Model(search);
		return errcode;
	}

	//initialize the audio device. this is hidden from the interface
	//because it should always be called through Init_Recognizer
	int Init_Mic()
	{
		if ((ad = ad_open_dev(cmd_ln_str_r(config, "-adcdev"),
			(int)cmd_ln_float32_r(config,
				"-samprate"))) == NULL)
		{
			//E_FATAL("Failed to open mic device\n");
			return -31;
		}
		if (ad_start_rec(ad) < 0)
		{
			//E_FATAL("Failed to start recording\n");
			return -32;
		}
		if (ps_start_utt(ps) < 0)
		{
			//E_FATAL("Failed to start utterance\n");
			return -33;
		}
		utt_started = FALSE;
		return 0;
	}

	//initialize the recognizer
	//I should allow the user to change settings on the fly by making a wrapper for ps_reinit
	DllExport int Init_Recognizer(int audio, int search, const char *hmm, 
		const char *lm, const char *dict, const char *jsgf, const char *kws)
	{
		int errcode = 0;
		//free any previously allocated objects, just to be safe
		if (Is_ad_Enabled() || Is_ps_Enabled() || Is_config_Enabled() || header->next != NULL)
			Free_Recognizer();
		errcode = Init_Decoder(audio, search, hmm, lm, dict, jsgf, kws);
		if (errcode != 0)
		{
			Free_Recognizer();
			//pocketsphinx decoder object failed to initialize.
			return errcode;
		}
		errcode = Init_Mic();
		//The mic failed to start properly.
		if (errcode != 0)
			Free_Recognizer();
		return errcode;
	}

	//run the recognizer on microphone. this needs to be called on loop with a 100ms break in between
	DllExport int Recognize_Mic()
	{
		if ((k = ad_read(ad, adbuf, 2048)) < 0)
			return -41;
		ps_process_raw(ps, adbuf, k, FALSE, FALSE);
		in_speech = ps_get_in_speech(ps);
		if (in_speech && !utt_started) {
			utt_started = TRUE;
		}
		if (!in_speech && utt_started) {
			/* speech -> silence transition, time to start new utterance  */
			ps_end_utt(ps);
			hyp = ps_get_hyp(ps, NULL);
			if (hyp != NULL) {
				char *newString = _strdup(hyp);
				Enqueue_String(newString);
			}
			if (ps_start_utt(ps) < 0)
				return -42;
			utt_started = FALSE;
		}
	}
}