using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace DataDownloader.ctg
{
    
    class Study
    {
        
    }

    public class CTGRecord
    {
        public int id { get; set; }
        
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
