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
using System.Threading.Tasks.Dataflow;

namespace DataDownloader.euctr
{
	class EUCTR_Controller
	{
		ScrapingBrowser browser;
		EUCTR_Processor processor;
		Source source;
		FileWriter file_writer;
		string file_base;
		int saf_id;
		int source_id;
		int sf_type_id;
		LoggingDataLayer logging_repo;


		public EUCTR_Controller(ScrapingBrowser _browser, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			processor = new EUCTR_Processor();
			source = _source;
			file_base = source.local_folder;
			source_id = source.id;
			saf_id = _saf_id;
			file_writer = new FileWriter(source);
			logging_repo = _logging_repo;
			sf_type_id = args.type_id;
		}


		public DownloadResult LoopThroughPages()
		{
			// consider type - from args
		    // if sf_type = 141, 142, 143 (normally 142 here) only download files 
		    // not already marked as 'complete' - i.e. very unlikely to change. This is 
		    // signalled by including the flag in the call to the processor routine.
			
			bool incomplete_only = (sf_type_id == 141 || sf_type_id == 142 || sf_type_id == 143);
			DownloadResult res = new DownloadResult();
			XmlSerializer writer = new XmlSerializer(typeof(EUCTR_Record));
			string baseURL = "https://www.clinicaltrialsregister.eu/ctr-search/search?query=&page=";

			WebPage homePage = browser.NavigateToPage(new Uri(baseURL));
			int rec_num = processor.GetListLength(homePage);
			if (rec_num != 0)
			{
				int loop_limit = rec_num % 20 == 0 ? rec_num / 20 : (rec_num / 20) + 1;
				for (int i = 714; i <= loop_limit; i++)
				{
					// Go to the summary page indicated by current value of i
					// Each page has up to 20 listed studies.
					// Once on that page each of the studies is processed in turn...
					homePage = browser.NavigateToPage(new Uri(baseURL + i.ToString()));
					List<EUCTR_Summmary> summaries = processor.GetStudySuummaries(homePage);

					foreach (EUCTR_Summmary s in summaries)
                    {
						// Check the euctr_id (sd_id) is not 'assumed complete' if only incomplete r
						// records are being considered; only proceed if this is the case

						res.num_checked++;
						StudyFileRecord file_record = logging_repo.FetchStudyFileRecord(s.eudract_id, source_id);

						if (!incomplete_only || file_record == null || file_record.assume_complete != true)
                        {
							// transfer summary details to the main EUCTR_record object
							EUCTR_Record st = new EUCTR_Record(s);

							WebPage detailsPage = null;
							try
							{
								detailsPage = browser.NavigateToPage(new Uri(st.details_url));
							}
                            catch(Exception e)
							{
								string eres = e.Message;
								StringHelpers.SendError("Problem in navigating to protocol details: " + eres + " id is " + s.eudract_id);
                            }
							if (detailsPage != null)
							{
								st = processor.ExtractProtocolDetails(st, detailsPage);
							}

							// Get results details

							if (st.results_url != null)
							{
								System.Threading.Thread.Sleep(800);
								WebPage resultsPage = browser.NavigateToPage(new Uri(st.results_url));

								if (resultsPage != null)
								{
									try
									{
										st = processor.ExtractResultDetails(st, resultsPage);
									}
									catch (Exception e)
									{
										string eres = e.Message;
										StringHelpers.SendError("Problem in navigating to result details: " + eres + " id is " + s.eudract_id);
									}
								}
							}

							// Write out study record as XML.
							if (!Directory.Exists(file_base))
							{
								Directory.CreateDirectory(file_base);
							}
							string file_name = "EU " + st.eudract_id + ".xml";
							string full_path = Path.Combine(file_base, file_name);
							file_writer.WriteEUCTRFile(writer, st, full_path);

							bool assume_complete = false;
							if (st.trial_status == "Completed" && st.results_url != null)
							{
								assume_complete = true;
							}
							bool added = logging_repo.UpdateStudyDownloadLogWithCompStatus(source_id, st.eudract_id, 
								                               st.details_url, saf_id,
															   assume_complete, full_path);
							res.num_downloaded++;
							if (added) res.num_added++;

							System.Threading.Thread.Sleep(800);
						}

						if (res.num_checked % 10 == 0) StringHelpers.SendFeedback("EUCTR pages checked: " + res.num_checked.ToString());
					}
				}
			}

            return res;
		}
		
	}
}
