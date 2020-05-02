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
    public class BioLINCC_Controller
    {
		ScrapingBrowser browser;
		DataLayer repo;
		BioLINCC_Processor processor;
		string file_base;
		FileWriter file_writer;
		int last_sf_id;
		int source_id;

		public BioLINCC_Controller(ScrapingBrowser _browser, DataLayer _repo, int _last_sf_id, int _source_id)
		{
			browser = _browser;
			repo = _repo;
			file_base = repo.GetBioLinccFolderBase();
			processor = new BioLINCC_Processor();
			file_writer = new FileWriter(_repo);
			last_sf_id = _last_sf_id;
			source_id = _source_id;
		}


		public void LoopThroughPages()
		{
			// biolincc start page
			WebPage homePage = browser.NavigateToPage(new Uri("https://biolincc.nhlbi.nih.gov/studies/"));

			int study_id = 1000;  // arbitrary start value
			var study_list_table = homePage.Find("div", By.Class("table-responsive"));
			HtmlNode[] studyRows = study_list_table.CssSelect("tbody tr").ToArray();
			XmlSerializer writer = new XmlSerializer(typeof(BioLINCC_Record));

			foreach (HtmlNode row in studyRows)
			{
				study_id++;
				// if (study_id < 1048) continue;  // continuing after a break	
				// if (study_id > 1010) break;     // testing

				// fetch the constructed study record
				BioLINCC_Record st = processor.GetStudyDetails(browser, repo, study_id, row);

				if (st != null)
				{
					// Write out study record as XML
					string file_name = st.sd_id + ".xml";
					string full_path = Path.Combine(file_base, file_name);
					file_writer.WriteBioLINCCFile(writer, st, full_path);
					file_writer.UpdateDownloadLog(study_id, source_id, st.sd_id, st.remote_url, last_sf_id,
													  st.last_revised_date, full_path);

					// put a pause here if necessary
					System.Threading.Thread.Sleep(1000);
				}
			}
		}
    }



	class Yoda_Controller
	{

		ScrapingBrowser browser;
		DataLayer repo;
		Yoda_Processor processor;
		string file_base;
		FileWriter file_writer;
		int last_sf_id;
		int source_id;

		public Yoda_Controller(ScrapingBrowser _browser, DataLayer _repo, int _last_sf_id, int _source_id)
		{
			browser = _browser;
			repo = _repo;
			file_base = repo.GetYodaFolderBase();
			processor = new Yoda_Processor();
			file_writer = new FileWriter(_repo);
			last_sf_id = _last_sf_id;
			source_id = _source_id;
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
				Yoda_Record st = processor.GetStudyDetails(browser, repo, page, sm);

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
