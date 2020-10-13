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
using System.Xml;
using System.Xml.XPath;

namespace DataDownloader.ctg
{
    public class CTG_Processor
    {
        public ctg_basics ObtainBasicDetails(XmlNode fs)
        {
            ctg_basics ctg = new ctg_basics();

            // need the identity and status modules from the protocol section
            string protocol_path = "Struct [@Name='Study']/Struct [@Name='ProtocolSection']/";
            string id_path = "Struct [@Name='IdentificationModule']/Field [@Name='NCTId']";
            string sd_sid = (fs.SelectSingleNode(protocol_path + id_path)).InnerText;

            string last_updated_path = "Struct [@Name='StatusModule']/Struct [@Name='LastUpdatePostDateStruct']/Field [@Name='LastUpdatePostDate']";
            string last_updated = (fs.SelectSingleNode(protocol_path + last_updated_path)).InnerText;

            ctg.sd_sid = sd_sid;
            ctg.last_updated = DateHelpers.FetchDateTimeFromDateString(last_updated);
            ctg.file_name = sd_sid + ".xml";
            ctg.file_path = sd_sid.Substring(0, 7) + "xxxx";
            ctg.remote_url = "https://clinicaltrials.gov/ct2/show/" + sd_sid;

            return ctg;
        }
    }
}
