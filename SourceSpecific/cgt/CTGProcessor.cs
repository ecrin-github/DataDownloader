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
using System.Web;
using System.Globalization;

namespace DataDownloader.ctg
{
    public class CTG_Processor
    {

        public void GetStudyDetails(ScrapingBrowser browser, WebPage homePage, LoggingDataLayer logging_repo, 
                                    int pagenum, string file_base, int sf_id)
        {

            // gets the details of each trial registry record
            // listed on the search page (n=100)

           

            //repo.StoreDatasetProperties(CopyHelpers.dataset_properties_helper,
                                       //  s.dataset_properties);
        }


        public void GetFullDetails(ref CTGRecord st, WebPage detailsPage, LoggingDataLayer logging_repo,
                                    int pagenum, string file_base)
        {

           

        }

        public string InnerContent(HtmlNode node)
        {
            if (node == null)
            {
                return "";
            }
            else
            {
                string allInner = node.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
                return HttpUtility.HtmlDecode(allInner);
            }
        }


        public string InnerLargeContent(HtmlNode node)
        {
            if (node == null)
            {
                return "";
            }
            else
            {
                string allInner = node.InnerHtml?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
                return HttpUtility.HtmlDecode(allInner);
            }
        }


        public DateTime? GetDate(HtmlNode node)
        {
            string date = node.InnerText?.Replace("\n", "")?.Replace("\r", "")?.Trim() ?? "";
            if (DateTime.TryParseExact(date, "dd/MM/yyyy", CultureInfo.InvariantCulture, 
                DateTimeStyles.None, out DateTime dt_value))
            {
                return dt_value;
            }
            else
            {
                return null;
            }
        }
    }
}
