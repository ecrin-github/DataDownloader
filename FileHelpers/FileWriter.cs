﻿using System;
using System.IO;
using System.Xml.Serialization;
using DataDownloader.biolincc;
using DataDownloader.yoda;


namespace DataDownloader
{
    public class FileWriter
    {
		Source source;

		public FileWriter(Source _source)
		{
			source = _source;
		}


		public void WriteBioLINCCFile(XmlSerializer writer, BioLinccRecord st, string full_path)
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

	}
}