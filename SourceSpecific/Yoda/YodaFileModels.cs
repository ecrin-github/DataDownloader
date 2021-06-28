using System;
using System.Collections.Generic;


namespace DataDownloader.yoda
{
    public class Yoda_Record
    {
        public string sd_sid { get; set; }
        public string registry_id { get; set; }
        public string display_title { get; set; }
        public string yoda_title { get; set; }
        public string brief_description { get; set; }
        public bool is_yoda_only { get; set; }
        public string remote_url { get; set; }
        public int? sponsor_id { get; set; }
        public string sponsor { get; set; }
        public int? study_type_id { get; set; }
        public string compound_generic_name { get; set; }
        public string compound_product_name { get; set; }
        public string therapaeutic_area { get; set; }
        public string enrolment { get; set; }
        public string percent_female { get; set; }
        public string percent_white { get; set; }
        public string product_class { get; set; }
        public string sponsor_protocol_id { get; set; }
        public string data_partner { get; set; }
        public string conditions_studied { get; set; }
        public string primary_citation_link { get; set; }
        public DateTime? last_revised_date { get; set; }

        public List<SuppDoc> supp_docs { get; set; }
        public List<Identifier> study_identifiers { get; set; }
        public List<Title> study_titles { get; set; }
        public List<Reference> study_references { get; set; }

        public Yoda_Record()
        {
        }

        public Yoda_Record(Summary sm)
        {
            sd_sid = sm.sd_sid;
            registry_id = sm.registry_id ?? "";
            yoda_title = sm.study_name;
            is_yoda_only = (registry_id.StartsWith("NCT") || registry_id.StartsWith("ISRCTN")) ? false : true;
            remote_url = sm.details_link;
        }

    }


    public class Summary
    {
        public string sd_sid { get; set; }
        public string registry_id { get; set; }
        public string generic_name { get; set; }
        public string study_name { get; set; }
        public string details_link { get; set; }
        public string enrolment_num { get; set; }
        public string csr_link { get; set; }
    }


    public class SponsorDetails
    {
        public int? org_id { get; set; }
        public string org_name { get; set; }
    }


    public class StudyDetails
    {
        public string sd_sid { get; set; }
        public string display_title { get; set; }
        public string brief_description { get; set; }
        public int? study_type_id { get; set; }
    }


    public class Title
    {
        public string sd_id { get; set; }
        public string title_text { get; set; }
        public int? title_type_id { get; set; }
        public string title_type { get; set; }
        public bool is_default { get; set; }
        public string comments { get; set; }

        public Title(string _sd_id, string _title_text, int? _title_type_id, string _title_type, bool _is_default, string _comments)
        {
            sd_id = _sd_id;
            title_text = _title_text;
            title_type_id = _title_type_id;
            title_type = _title_type;
            is_default = _is_default;
            comments = _comments;
        }

        public Title()
        { }
    }


    public class Identifier
    {
        public string identifier_value { get; set; }
        public int? identifier_type_id { get; set; }
        public string identifier_type { get; set; }
        public int? identifier_org_id { get; set; }
        public string identifier_org { get; set; }

        public Identifier()
        { }

        public Identifier(string _identifier_value,
            int? _identifier_type_id, string _identifier_type,
            int? _identifier_org_id, string _identifier_org)
        {
            identifier_value = _identifier_value;
            identifier_type_id = _identifier_type_id;
            identifier_type = _identifier_type;
            identifier_org_id = _identifier_org_id;
            identifier_org = _identifier_org;
        }
    }


    public class Reference
    {
        public string pmid { get; set; }

        public Reference(string _pmid)
        {
            pmid = _pmid;
        }

        public Reference()
        { }
    }


    public class SuppDoc
    {
        public string doc_name { get; set; }
        public string comment { get; set; }
        public string url { get; set; }

        public SuppDoc(string _doc_name)
        {
            doc_name = _doc_name;
        }

        public SuppDoc()
        { }

    }


    public class UnregisteredStudy
    {
        public int id { get; set; }
        public string nct_number { get; set; }
        public string sd_id { get; set; }
        public string title { get; set; }
        public string remote_url { get; set; }
        public string sponsor_protocol_id { get; set; }
    }



}
