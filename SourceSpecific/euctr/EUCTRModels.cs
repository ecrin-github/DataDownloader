using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace DataDownloader.euctr
{

    public class EUCTR_Record
    {
        public string eudract_id { get; set; }
        public string sponsor_id { get; set; }
        public string sponsor_name { get; set; }
        public string start_date { get; set; }
        public string competent_authority { get; set; }
        public string trial_type { get; set; }
        public string trial_status { get; set; }
        public string medical_condition { get; set; }
        public string population_age { get; set; }
        public string gender { get; set; }
        public string details_url { get; set; }
        public string results_url { get; set; }
        public string results_version { get; set; }
        public string results_first_date { get; set; }
        public string results_revision_date { get; set; }
        public string results_summary_link { get; set; }
        public string results_summary_name { get; set; }
        public string results_pdf_link { get; set; }
        public string entered_in_db { get; set; }

        public List<MeddraTerm> meddra_terms { get; set; }
        public List<DetailLine> identifiers { get; set; }
        public List<DetailLine> sponsors { get; set; }
        public List<ImpLine> imps { get; set; }
        public List<DetailLine> features { get; set; }
        public List<DetailLine> population { get; set; }

        public EUCTR_Record(EUCTR_Summmary s)
        {
            eudract_id = s.eudract_id;
            sponsor_id = s.sponsor_id;
            sponsor_name = s.sponsor_name;
            start_date = s.start_date;
            medical_condition = s.medical_condition;
            population_age = s.population_age;
            gender = s.gender;
            trial_status = s.trial_status;
            details_url = s.details_url;
            results_url = s.results_url;
            meddra_terms = s.meddra_terms;
    }

    public EUCTR_Record()
        { }
    }

    public class EUCTR_Summmary
    {
        public string eudract_id { get; set; }
        public string sponsor_id { get; set; }
        public string sponsor_name { get; set; }
        public string start_date { get; set; }
        public string medical_condition { get; set; }
        public string population_age { get; set; }
        public string gender { get; set; }
        public string trial_status { get; set; }        
        public string details_url { get; set; }
        public string results_url { get; set; }

        public List<MeddraTerm> meddra_terms { get; set; }

        public EUCTR_Summmary(string _eudract_id, string _sponsor_id, string _start_date)
        {
            eudract_id = _eudract_id;
            sponsor_id = _sponsor_id;
            start_date = _start_date;
        }
    }


    public class MeddraTerm
    {
        public string version { get; set; }
        public string soc_term { get; set; }
        public string code { get; set; }
        public string term { get; set; }
        public string level { get; set; }
    }


    public class DetailLine
    {
        public string item_code { get; set; }
        public string item_name { get; set; }
        public int item_number { get; set; }

        [XmlArray("values")]
        [XmlArrayItem("value")]
        public List<item_value> item_values { get; set; }
    }

    public class ImpLine
    {
        public int imp_number { get; set; }
        public string item_code { get; set; }
        public string item_name { get; set; }
        public int item_number { get; set; }

        [XmlArray("values")]
        [XmlArrayItem("value")]
        public List<item_value> item_values { get; set; }
    }

    public class item_value
    {
        [XmlText]
        public string value { get; set; }

        public item_value(string _value)
        {
            value = _value;
        }

        public item_value()
        { }
    }


    public class file_record
    {
        public int id { get; set; }
        public string local_path { get; set; }

    }

}
