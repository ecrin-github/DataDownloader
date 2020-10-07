using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DataDownloader.biolincc
{
    public class BioLINCC_Controller
    {
		ScrapingBrowser browser;
		BioLinccDataLayer biolincc_repo;
		BioLINCC_Processor processor;
		Source source;
		string file_base;
		FileWriter file_writer;
		int saf_id;
		int source_id;
		LoggingDataLayer logging_repo;


		public BioLINCC_Controller(ScrapingBrowser _browser, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			biolincc_repo = new BioLinccDataLayer();
			processor = new BioLINCC_Processor();
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
			// For BioLincc, all data is downloaded each time during a download, as it takes a relatively short time
			// and the files simply replaced or - if new - added to the folder. There is therrefore not a concept of an
			// update or focused download, as opposed to a full download.
			
			// Get list of studies from the Biolincc start page.

			WebPage homePage = browser.NavigateToPage(new Uri("https://biolincc.nhlbi.nih.gov/studies/"));
			var study_list_table = homePage.Find("div", By.Class("table-responsive"));
			HtmlNode[] studyRows = study_list_table.CssSelect("tbody tr").ToArray();

			XmlSerializer writer = new XmlSerializer(typeof(BioLincc_Record));
			DownloadResult res = new DownloadResult();

			// Consider each study in turn.

			foreach (HtmlNode row in studyRows)
			{
				// fetch the constructed study record
				res.num_checked++;
				BioLincc_Record st = processor.GetStudyDetails(browser, biolincc_repo, res.num_checked, row);

				if (st != null)
				{
					// Write out study record as XML.

					string file_name = source.local_file_prefix + st.sd_sid + ".xml";
					string full_path = Path.Combine(file_base, file_name);
					file_writer.WriteBioLINCCFile(writer, st, full_path);
					bool added = logging_repo.UpdateStudyDownloadLog(source_id, st.sd_sid, st.remote_url, saf_id,
													  st.last_revised_date, full_path);
					res.num_downloaded++;
					if (added) res.num_added++;

					// Put a pause here if necessary.

					System.Threading.Thread.Sleep(1000);
				}

				StringHelpers.SendFeedback("Biolincc files checked: " + res.num_checked.ToString());
				StringHelpers.SendFeedback("Biolincc files downloaded: " + res.num_downloaded.ToString());
			}

			return res;
		}
    }
}
