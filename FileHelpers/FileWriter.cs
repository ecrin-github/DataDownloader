using System;
using System.IO;
using System.Xml.Serialization;
using DataDownloader.biolincc;
using DataDownloader.yoda;
using DataDownloader.isrctn;
using DataDownloader.euctr;
using DataDownloader.who;
using DataDownloader.vivli;


namespace DataDownloader
{
    public class FileWriter
    {
		Source source;

		public FileWriter(Source _source)
		{
			source = _source;
		}


		public void WriteBioLINCCFile(XmlSerializer writer, BioLincc_Record st, string full_path)
		{
			if (File.Exists(full_path))
			{
				File.Delete(full_path);
			}
			FileStream file = System.IO.File.Create(full_path);
			writer.Serialize(file, st);
			file.Close();
		}


		public void WriteYodaFile(XmlSerializer writer, Yoda_Record st, string full_path)
		{
			if (File.Exists(full_path))
			{
				File.Delete(full_path);
			}
			FileStream file = System.IO.File.Create(full_path);
			writer.Serialize(file, st);
			file.Close();
		}


		public void WriteISRCTNFile(XmlSerializer writer, ISCTRN_Record st, string full_path)
		{
			if (File.Exists(full_path))
			{
				File.Delete(full_path);
			}
			FileStream file = System.IO.File.Create(full_path);
			writer.Serialize(file, st);
			file.Close();
		}


		public void WriteEUCTRFile(XmlSerializer writer, EUCTR_Record st, string full_path)
		{
			if (File.Exists(full_path))
			{
				File.Delete(full_path);
			}
			FileStream file = System.IO.File.Create(full_path);
			writer.Serialize(file, st);
			file.Close();
		}


		public void WriteWHOSourcedFile(XmlSerializer writer, WHORecord st, string full_path)
		{
			if (File.Exists(full_path))
			{
				File.Delete(full_path);
			}
			FileStream file = System.IO.File.Create(full_path);
			writer.Serialize(file, st);
			file.Close();
		}

		public void WriteVivliFile(XmlSerializer writer, VivliRecord st, string full_path)
		{
			if (File.Exists(full_path))
			{
				File.Delete(full_path);
			}
			FileStream file = System.IO.File.Create(full_path);
			writer.Serialize(file, st);
			file.Close();
		}

	}
}
