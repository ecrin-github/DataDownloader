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


namespace DataDownloader.ctg
{
	class CTG_Controller
	{
    	ScrapingBrowser browser;
		CTG_Processor processor;
		Source source;
		string file_base;
		FileWriter file_writer;
		int sf_id;
		int source_id;
		LoggingDataLayer logging_repo;

		public CTG_Controller(ScrapingBrowser _browser, int _sf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			processor = new CTG_Processor();
			source = _source;
			file_base = source.local_folder;
			source_id = source.id;
			sf_id = _sf_id;
			file_writer = new FileWriter(source);
			logging_repo = _logging_repo;
		}
		

		public DownloadResult LoopThroughPages()
		{
    		// Data retrieval is through a file download (for a full dump of the CTG data) or 
			// download via an API call to revised files using a cut off revision date. 
			// The args parameter needs to be inspected to ddetermine which. If 9rrarerly) a specific non-date
			// criterion set is used, the corresponding API call will need to be instructed to match that call.
			// If a full download the zip file can simply be expanded into the CTG folder area.
			// If an update the new files will be added, the amended files replaced, as necessary.
			// In some cases a search may be carried out to identify the files without downloading them.
			
			string baseURL = "https://www.isrctn.com/search?q=&amp;page=";
			string endURL = "&amp;pageSize=100&amp;searchType=basic-search";

			//for (int i = 1; i < 2; i++)
			for (int i = 1; i < 196; i++)
			{
				WebPage homePage = browser.NavigateToPage(new Uri(baseURL + i.ToString() + endURL));
				processor.GetStudyDetails(browser, homePage, logging_repo, i, file_base, sf_id);


			}

			return null;
		}
	}
}
