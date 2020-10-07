using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

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
		string cut_off_date;

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
			cut_off_date = args.cutoff_date?.ToString("yyyy-MM-dd");
		}

		// Get the number of records required and set up the loop

		public DownloadResult LoopThroughPages()
		{
			// Construct the initial search string
			DownloadResult res = new DownloadResult();
			XmlSerializer writer = new XmlSerializer(typeof(ISCTRN_Record));

			string part1_of_url = "http://www.isrctn.com/search?pageSize=100&sort=&page=";
		    string initial_page_num = "1";
		    string part2_of_url = "&q=&filters=GT+lastEdited%3A";
			string end_of_url = "T00%3A00%3A00.000Z&searchType=advanced-search";

			string url = part1_of_url + initial_page_num + part2_of_url + cut_off_date + end_of_url;

			WebPage homePage = browser.NavigateToPage(new Uri(url));
			int rec_num = processor.GetListLength(homePage);
			if (rec_num != 0)
			{
				int loop_limit = rec_num % 100 == 0 ? rec_num / 100 : (rec_num / 100) + 1;

				for (int i = 1; i <= loop_limit; i++)
				{
					// Obtain and go through each page of 100 entries.

                    homePage = browser.NavigateToPage(new Uri(part1_of_url + i.ToString() + 
					                                  part2_of_url + cut_off_date + end_of_url));

					int n = 0;
					var pageContent = homePage.Find("ul", By.Class("ResultsList"));
					HtmlNode[] studyRows = pageContent.CssSelect("li article").ToArray();
					string ISRCTNNumber, remote_link;
					int colonPos;

					// Now process each study, one row at a time

					foreach (HtmlNode row in studyRows)
					{
						
						HtmlNode main = row.CssSelect(".ResultsList_item_main").FirstOrDefault();
						HtmlNode title = main.CssSelect(".ResultsList_item_title a").FirstOrDefault();
						if (title != null)
						{
							string titleString = title.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
                            if (titleString.Contains(":"))
                            {
                                // get ISRCTN id

                                colonPos = titleString.IndexOf(":");
                                ISRCTNNumber = titleString.Substring(0, colonPos - 1).Trim();
                               
                                n++;
								res.num_checked++;

								// only get details if not already downloaded
								StudyFileRecord sfr = logging_repo.FetchStudyFileRecord(ISRCTNNumber, source_id);
								if (sfr == null || sfr.last_downloaded < new DateTime(2020, 10, 04))
								{
									remote_link = "https://www.isrctn.com/" + ISRCTNNumber;

									// obtain details of that study
									// but pause every 10 accesses
									if (n % 10 == 0)
									{
										System.Threading.Thread.Sleep(2000);
									}
									ISCTRN_Record st = new ISCTRN_Record();
									WebPage detailsPage = browser.NavigateToPage(new Uri(remote_link));
									st = processor.GetFullDetails(detailsPage, ISRCTNNumber);

									// Write out study record as XML.
									if (!Directory.Exists(file_base))
									{
										Directory.CreateDirectory(file_base);
									}
									string file_name = st.isctrn_id + ".xml";
									string full_path = Path.Combine(file_base, file_name);

									file_writer.WriteISRCTNFile(writer, st, full_path);
									bool added = logging_repo.UpdateStudyDownloadLog(source_id, st.isctrn_id, remote_link, saf_id,
																	   st.last_edited, full_path);
									res.num_downloaded++;
									if (added) res.num_added++;
								}
							}
						}

						if (res.num_checked % 10 == 0) StringHelpers.SendFeedback(res.num_checked.ToString() + " files downloaded");
					}

				}
				
			}
			
			return res;
		}
	}
}
