using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using ScrapySharp.Html;
using System.IO;
using System.Xml.Serialization;
using System.Threading.Tasks.Dataflow;

namespace DataDownloader.euctr
{
	class EUCTR_Controller
	{
		ScrapingBrowser browser;
		EUCTR_Processor processor;
		Source source;
		string file_base;
		int saf_id;
		int source_id;
		int sf_type_id;
		LoggingDataLayer logging_repo;

		public EUCTR_Controller(ScrapingBrowser _browser, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			processor = new EUCTR_Processor();
			source = _source;
			file_base = source.local_folder;
			source_id = source.id;
			saf_id = _saf_id;
			logging_repo = _logging_repo;
			sf_type_id = args.type_id;
		}


		public DownloadResult LoopThroughPages()
		{
			// consider type - from args
		    // if sf_type = 141, 142, 143 (normally 142 here) only download files 
		    // not already marked as 'complete' - i.e. very unlikely to change. This is 
		    // signalled by including the flag in the call to the processor routine.
			
			bool incomplete_only = (sf_type_id == 141 || sf_type_id == 142 || sf_type_id == 143);
			string baseURL = "https://www.clinicaltrialsregister.eu/ctr-search/search?query=&page=";

			// Need a better way of getting the total number (e.g. from finding it on the first page...)

			for (int i = 0; i < 1852; i+=5)
			{
				for (int j = i; j < i+5; j++)
				{
					// Go to the summary page indicated by current vazlue of j
					// Each page has up to 20 listed studies.
			        // Once on that page each of the studies is processed in turn...

					// Needs a better loop structure here, to make the monitoring easier
					// e.g. initial loop returns up to 20 objects summaries, including URLs, which can then 
					// be downloaded within an inner loop

					WebPage homePage = browser.NavigateToPage(new Uri(baseURL + j.ToString()));
					processor.GetStudyInitialDetails(browser, homePage, logging_repo, j, file_base, source_id, saf_id, incomplete_only);
					Console.WriteLine(j.ToString());
				}
			}

			return null;

		}
		
	}
}
