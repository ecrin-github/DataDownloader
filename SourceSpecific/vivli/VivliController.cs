using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Xml.Serialization;

namespace DataDownloader.vivli
{
    public class Vivli_Controller
    {
		ScrapingBrowser browser;
		VivliDataLayer vivli_repo;
		Vivli_Processor processor;
		Source source;
		string file_base;
		FileWriter file_writer;
		int sf_id;
		int source_id;
		LoggingDataLayer logging_repo;


		public Vivli_Controller(ScrapingBrowser _browser, int _sf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			vivli_repo = new VivliDataLayer();
			processor = new Vivli_Processor();
			source = _source;
			file_base = source.local_folder;
			source_id = source.id;
			sf_id = _sf_id;
			file_writer = new FileWriter(source);
			logging_repo = _logging_repo;
		}


		public void FetchURLDetails()
		{
			// Set up initial study list
			// store it in pp table

			List<VivliURL> all_study_list = new List<VivliURL>();
			vivli_repo.SetUpParameterTable();

			string baseURL = "https://search.datacite.org/works?query=vivli&resource-type-id=dataset";
			WebPage startPage = browser.NavigateToPage(new Uri(baseURL));

			// Entries on DataCite search are 25 / page
			int totalNumber = processor.GetStudyNumbers(startPage);
			int loopEndNumber = (totalNumber / 25) + 2;

			// for (int i = 1; i < 5; i++)  // testing only
			for (int i = 1; i < loopEndNumber; i++)
			{
				string URL = baseURL + " &page=" + i.ToString();
				WebPage web_page = browser.NavigateToPage(new Uri(URL));

				List<VivliURL> page_study_list = processor.GetStudyInitialDetails(web_page, i);
				vivli_repo.StoreRecs(CopyHelpers.api_url_copyhelper, page_study_list);

				// Log to console and pause before the next page

				Console.WriteLine(i.ToString());
				System.Threading.Thread.Sleep(1000);
			}
		}

		public void LoopThroughPages()
		{

			// Go through the vivli data, fetcvhing the stored urls
			// and using these to call the api directly, receiving json
			// that can be extracted directly from the response

			vivli_repo.SetUpStudiesTable();
			vivli_repo.SetUpPackagesTable();
			vivli_repo.SetUpDataObectsTable();

			IEnumerable<VivliURL> all_study_list = vivli_repo.FetchVivliApiUrLs();

			foreach (VivliURL s in all_study_list)
            {
				processor.GetAndStoreStudyDetails(s, vivli_repo);

				// logging to go here

				// write to console...
				Console.WriteLine(s.id.ToString() + ": " + s.vivli_url);

				// put a pause here if necessary
    			System.Threading.Thread.Sleep(800);

			}
		}
    }
}
