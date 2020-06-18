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
		int sf_id;
		int source_id;
		LoggingDataLayer logging_repo;

		public Yoda_Controller(ScrapingBrowser _browser, int _sf_id, Source _source, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			yoda_repo = new YodaDataLayer();
			processor = new Yoda_Processor();
			source = _source;
			file_base = source.local_folder;
			source_id = source.id;
			sf_id = _sf_id;
			file_writer = new FileWriter(source);
			logging_repo = _logging_repo;
		}

		public void LoopThroughPages()
		{
			// set up initial study list
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

			// then consider each study in turn
			XmlSerializer writer = new XmlSerializer(typeof(Yoda_Record));
			int seqnum = 0;
			foreach (Summary sm in all_study_list)
			{
				// get the details page...
				seqnum++;
				WebPage studyPage = browser.NavigateToPage(new Uri(sm.details_link));

				// send the page off for processing
				HtmlNode page = studyPage.Find("div", By.Class("region-content")).FirstOrDefault();
				Yoda_Record st = processor.GetStudyDetails(browser, yoda_repo, page, sm);

				if (st != null)
				{
					// Write out study record as XML
					string file_name = source.local_file_prefix + st.sd_sid + ".xml";
					string full_path = Path.Combine(file_base, file_name);
					file_writer.WriteYodaFile(writer, st, full_path);
					logging_repo.UpdateDownloadLog(seqnum, source_id, st.sd_sid, st.remote_url, sf_id,
													  st.last_revised_date, full_path);

					// put a pause here if necessary
					System.Threading.Thread.Sleep(500);
				}
			}
		}
	}

}
