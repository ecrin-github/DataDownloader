using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DataDownloader
{
    class Yoda_Controller
	{

		ScrapingBrowser browser;
		DataLayer repo;
		YodaDataLayer yoda_repo;
		Yoda_Processor processor;
		string file_base;
		FileWriter file_writer;
		int last_sf_id;
		int source_id;

		public Yoda_Controller(ScrapingBrowser _browser, DataLayer _repo, int _last_sf_id, int _source_id)
		{
			browser = _browser;
			repo = _repo;
			yoda_repo = new YodaDataLayer();
			processor = new Yoda_Processor();
			file_writer = new FileWriter(_repo);
			last_sf_id = _last_sf_id;
			source_id = _source_id;
			file_base = repo.GetYodaFolderBase();
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
					string file_name = "yoda" + st.sd_id + ".xml";
					string full_path = Path.Combine(file_base, file_name);
					file_writer.WriteYodaFile(writer, st, full_path);
					file_writer.UpdateDownloadLog(seqnum, source_id, st.sd_id, st.remote_url, last_sf_id,
													  st.last_revised_date, full_path);

					// put a pause here if necessary
					System.Threading.Thread.Sleep(500);
				}
			}
		}
	}

}
