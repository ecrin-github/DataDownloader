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


namespace DataDownloader.isrctn
{
	class ISRCTN_Controller
	{
    	ScrapingBrowser browser;
		ISRCTN_Processor processor;
		Source source;
		string file_base;
		FileWriter file_writer;
		int saf_id;
		int source_id;
		LoggingDataLayer logging_repo;

		public ISRCTN_Controller(ScrapingBrowser _browser, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			processor = new ISRCTN_Processor();
			source = _source;
			file_base = source.local_folder;
			source_id = source.id;
			saf_id = _saf_id;
			file_writer = new FileWriter(source);
			logging_repo = _logging_repo;
		}
		//static List<Study> studies = new List<Study>();


		public DownloadResult LoopThroughPages()
		{
    		string baseURL = "https://www.isrctn.com/search?q=&amp;page=";
			string endURL = "&amp;pageSize=100&amp;searchType=basic-search";

			//for (int i = 1; i < 2; i++)
			for (int i = 1; i < 196; i++)
			{
				WebPage homePage = browser.NavigateToPage(new Uri(baseURL + i.ToString() + endURL));
				processor.GetStudyDetails(browser, homePage, logging_repo, i, file_base, saf_id);
			}

			return null;
		}
	}
}
