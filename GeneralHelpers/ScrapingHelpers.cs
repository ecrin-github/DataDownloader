using DataDownloader.yoda;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using ScrapySharp.Network;
using System;
using System.Threading.Tasks;
using System.Net.Http;

namespace DataDownloader
{

    public class ScrapingHelpers
    {
        LoggingDataLayer logging_repo;
        ScrapingBrowser browser;
        HttpClient webClient;

        public ScrapingHelpers(ScrapingBrowser _browser, LoggingDataLayer _logging_repo)
        {
            logging_repo = _logging_repo;
            browser = _browser;
            webClient = null;
        }

        public ScrapingHelpers(LoggingDataLayer _logging_repo)
        {
            logging_repo = _logging_repo;
            browser = null;
            webClient = new HttpClient();
        }

        public WebPage GetPage(string url)
        {
            try
            {
                return browser.NavigateToPage(new Uri(url));
            }
            catch (Exception e)
            {
                logging_repo.LogError("Error in obtaining page from " + url + ": " + e.Message);
                return null;
            }
        }

        public async Task<string> GetStringFromURLAsync(string url)
        {
            try
            {
                string result = await webClient.GetStringAsync(url);
                return result;
            }
            catch (Exception e)
            {
                logging_repo.LogError("Problem in using webClient.GetStringAsync at "
                                       + url + ": " + e.Message);
                return null;
            }
        }

        public SuppDoc FindSuppDoc(List<SuppDoc> supp_docs, string name)
        {
            SuppDoc sd = null;
            foreach (SuppDoc s in supp_docs)
            {
                if (s.doc_name == name)
                {
                    sd = s;
                    break;
                }
            }
            return sd;
        }


        public string AttValue(string inputText, string attribute_name, HtmlNode entrySupp)
        {
            // drop the attribute name from the text
            string attValue = inputText.Replace(attribute_name, "");

            // drop any supplementary entry title
            if (entrySupp != null)
            {
                attValue = attValue.Replace(entrySupp.InnerText, "");
            }

            // drop carriage returns and trim 
            return attValue.Replace("\n", "").Replace("\r", "").Trim();
        }

        
        public string GetPMIDFromNLM(string pmc_id)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            string base_url = "https://www.ncbi.nlm.nih.gov/pmc/utils/idconv/v1.0/";
            base_url += "?tool=ECRIN-MDR&email=steve@canhamis.eu&versions=no&ids=";
            string query_url = base_url + pmc_id + "&format=json";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(query_url);
            request.Method = "GET";
            try
            {

                WebResponse response = request.GetResponse();

                // assumes response is in utf-8
                MemoryStream ms = new MemoryStream();
                response.GetResponseStream().CopyTo(ms);
                byte[] response_data = ms.ToArray();

                PMCResponse PMC_object = JsonSerializer.Deserialize<PMCResponse>(response_data, options);
                return PMC_object?.records[0]?.pmid;
            }
            catch (Exception e)
            {
                logging_repo.LogError("Error in obtaining pmid from PMC page" + pmc_id + ": " + e.Message);
                return null;
            }
        }


        public string  GetPMIDFromPage(string citation_link)
        {
            string pmid = "";
            WebPage page = GetPage(citation_link);
            if (page != null)
            {
                // only works with pmid pages, that have this dl tag....
                HtmlNode ids_div = page.Find("dl", By.Class("rprtid")).FirstOrDefault();
                if (ids_div != null)
                {
                    HtmlNode[] dts = ids_div.CssSelect("dt").ToArray();
                    HtmlNode[] dds = ids_div.CssSelect("dd").ToArray();

                    if (dts != null && dds != null)
                    {
                        for (int i = 0; i < dts.Length; i++)
                        {
                            string dts_type = dts[i].InnerText.Trim();
                            if (dts_type == "PMID:")
                            {
                                pmid = dds[i].InnerText.Trim();
                            }
                        }
                    }
                }
            }
            return pmid;
        }


    }
}
