using System;
using System.Collections.Generic;

namespace DataDownloader.isrctn
{
  
    public class ISCTRN_Study
    {
        public int id { get; set; }
        public string ISRCTN_number { get; set; }
        public string study_name { get; set; }
        public string remote_link { get; set; }
        
        public ISCTRN_Study()
        { }

        public ISCTRN_Study(int _id, string _ISRCTN_number, string _study_name, string _remote_link)
        {
            id = _id;
            ISRCTN_number = _ISRCTN_number;
            study_name = _study_name;
            remote_link = _remote_link;
        }
    }
  

    public class ISCTRN_Record
    {
        public string isctrn_id { get; set; }
        public string doi { get; set; }
        public string study_name { get; set; }
        public string condition_category { get; set; }
        public DateTime? submission_date { get; set; }
        public DateTime? registration_date { get; set; }
        public DateTime? last_edited { get; set; }
        public string registration_type { get; set; }
        public string trial_status { get; set; }
        public string recruitment_status { get; set; }
        public string background { get; set; }
        public string trial_website { get; set; }
        public string website_link { get; set; }

        public List<Item> contacts { get; set; }
        public List<Item> identifiers { get; set; }
        public List<Item> study_info { get; set; }
        public List<Item> eligibility { get; set; }
        public List<Item> locations { get; set; }
        public List<Item> sponsor { get; set; }
        public List<Item> funders { get; set; }
        public List<Item> publications { get; set; }
        public List<Item> additional_files { get; set; }
        public List<Item> notes { get; set; }
        public List<Output> outputs { get; set; }
    }

    public class Item
    {
        public int seq_id { get; set; }
        public string item_name { get; set; }
        public string item_value { get; set; }

        public Item(int _seq_id, string _item_name, string _item_value)
        {
            seq_id = _seq_id;
            item_name = _item_name;
            item_value = _item_value;
        }

        public Item()
        { }
    }

    public class Output
    {
        public string output_type { get; set; }
        public string output_url { get; set; }
        public string details { get; set; }
        public DateTime? date_created { get; set; }
        public DateTime? date_added { get; set; }
        public string peer_reviewed { get; set; }
        public string patient_facing { get; set; }

        public Output()
        { }

        public Output(string _output_type, string _output_url, string _details, DateTime? _date_created,
            DateTime? _date_added , string _peer_reviewed , string _patient_facing)
        {
            output_type = _output_type;
            output_url = _output_url;
            details = _details;
            date_created = _date_created;
            date_added = _date_added;
            peer_reviewed = _peer_reviewed;
            patient_facing = _patient_facing;

        }
    }


}
