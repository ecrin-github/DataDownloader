using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DataDownloader.yoda
{
    class Yoda_Controller
	{
		ScrapingBrowser browser;
		YodaDataLayer yoda_repo;
		Yoda_Processor processor;
		Source source;		
		string file_base;
		FileWriter file_writer;
		int saf_id;
		int source_id;
		LoggingDataLayer logging_repo;

		public Yoda_Controller(ScrapingBrowser _browser, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			yoda_repo = new YodaDataLayer();
			processor = new Yoda_Processor();
			source = _source;
			file_base = source.local_folder;
			source_id = source.id;
			saf_id = _saf_id;
			file_writer = new FileWriter(source);
			logging_repo = _logging_repo;
		}

		public DownloadResult LoopThroughPages()
		{
			// Although the args parameter is passed in for consistency it is not used.
			// For Yoda, all data is downloaded each time during a download, as it takes a relatively short time
			// and the files simply replaced or - if new - added to the folder. There is therrefore not a concept of an
			// update or focused download, as opposed to a full download.

			// set up initial study list.

			List<Summary> all_study_list = new List<Summary>();
			string baseURL = "https://yoda.yale.edu/trials-search?amp%3Bpage=0&field_clintrials_gov_nct_number_title=&page=";
			//for (int i = 0; i < 4; i++)
			for (int i = 0; i < 42; i++)
			{
				WebPage homePage = browser.NavigateToPage(new Uri(baseURL + i.ToString()));
				List<Summary> page_study_list = processor.GetStudyInitialDetails(homePage, i);
				all_study_list.AddRange(page_study_list);

				Console.WriteLine(i.ToString());
				System.Threading.Thread.Sleep(300);
			}

			DownloadResult res = new DownloadResult();
			XmlSerializer writer = new XmlSerializer(typeof(Yoda_Record));

			// Consider each study in turn.

			foreach (Summary sm in all_study_list)
			{
				// Get the details page...
				
				WebPage studyPage = browser.NavigateToPage(new Uri(sm.details_link));
                res.num_checked++;

				// Send the page off for processing

				HtmlNode page = studyPage.Find("div", By.Class("region-content")).FirstOrDefault();
				Yoda_Record st = processor.GetStudyDetails(browser, yoda_repo, page, sm);

				if (st != null)
				{
					// Write out study record as XML.

					string file_name = source.local_file_prefix + st.sd_sid + ".xml";
					string full_path = Path.Combine(file_base, file_name);
					file_writer.WriteYodaFile(writer, st, full_path);
					bool added = logging_repo.UpdateStudyDownloadLog(source_id, st.sd_sid, st.remote_url, saf_id,
													  st.last_revised_date, full_path);
					res.num_downloaded++;
					if (added) res.num_added++;

					// Put a pause here if necessary.

					System.Threading.Thread.Sleep(500);
				}

				Console.WriteLine(res.num_checked.ToString());
     		}

			return res;
		}
	}
}
