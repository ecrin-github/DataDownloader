using System;
using System.Collections.Generic;

namespace DataDownloader.isrctn
{

    class Study
    {
        public int Id { get; set; }
        public string ISRCTNNumber { get; set; }
        public string StudyName { get; set; }
        public string OverallStatus { get; set; }
        public string RecruitmentStatus { get; set; }
        public string DateAssigned { get; set; }
    }

    public class ISCTRN_Record
    {
        public int id { get; set; }
        public string isctrn_id { get; set; }
        public string doi { get; set; }
        public string study_name { get; set; }
        public string condition_category { get; set; }
        public DateTime? date_assigned { get; set; }
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
}
