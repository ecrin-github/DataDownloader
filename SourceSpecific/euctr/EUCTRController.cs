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

namespace DataDownloader.euctr
{
	class EUCTR_Controller
	{
		ScrapingBrowser browser;
		EUCTR_Processor processor;
		Source source;
		string file_base;
		FileWriter file_writer;
		int sf_id;
		int source_id;
		LoggingDataLayer logging_repo;

		public EUCTR_Controller(ScrapingBrowser _browser, int _sf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			processor = new EUCTR_Processor();
			source = _source;
			file_base = source.local_folder;
			source_id = source.id;
			sf_id = _sf_id;
			file_writer = new FileWriter(source);
			logging_repo = _logging_repo;
		}

		public DownloadResult LoopThroughPages()
		{
			string baseURL = "https://www.clinicaltrialsregister.eu/ctr-search/search?query=&page=";

			for (int i = 0; i < 1852; i+=5)
			{
				for (int j = i; j < i+5; j++)
				{
					WebPage homePage = browser.NavigateToPage(new Uri(baseURL + j.ToString()));
					processor.GetStudyInitialDetails(browser, homePage, logging_repo, j, file_base, source_id, sf_id);
					Console.WriteLine(j.ToString());
				}
			}




			return null;

		}
		
	}
}
