﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace pdf_reducer {
    class Program {

        const int MB = 1048576;
        const string SRC = @"c:\Users\Jacob\Documents\pdf_reducer\src\";
        const string DEST = @"c:\Users\Jacob\Documents\pdf_reducer\dest\";

        static string create_dest_name(FileInfo file, string filename) {
            var new_file_name = filename;
            var count = 0;
            while (File.Exists(new_file_name)) {
                new_file_name = DEST + file.Name.Replace(file.Extension, "_" + ++count) + file.Extension;
            }
            return new_file_name;
        }

        static long get_file_size(string filepath) {
            if (!File.Exists(filepath)) {
                return 0;
            }

            return new FileInfo(filepath).Length;
        }

        static void unembed_font(PdfDictionary dict) {
            if (!dict.IsFont()) {
                return;
            }

            if (dict.GetAsDict(PdfName.FONTFILE2) != null) {
                return;
            }

            var basefont = dict.GetAsName(PdfName.BASEFONT);
            if (basefont.GetBytes().Length > 7 && basefont.GetBytes()[7] == '+') {
                basefont = new PdfName(basefont.ToString().Substring(8));
                dict.Put(PdfName.BASEFONT, basefont);
            }

            var fd = dict.GetAsDict(PdfName.FONTDESCRIPTOR);

            if (fd == null) {
                return;
            }

            fd.Put(PdfName.FONTNAME, basefont);
            fd.Remove(PdfName.FONTFILE);

        }

        static long remove_embedded_fonts(FileInfo file) {
            try {
                var filename = create_dest_name(file, DEST + file.Name);

                using (var reader = new PdfReader(file.FullName)) {
                    PdfObject obj;

                    for (int i = 1; i < reader.XrefSize; ++i) {
                        obj = reader.GetPdfObject(i);

                        if (obj == null || !obj.IsDictionary()) {
                            continue;
                        }
                        unembed_font((PdfDictionary)obj);
                    }
                    var fs = new FileStream(filename, FileMode.Create);
                    var stamp = new PdfStamper(reader, fs);
                    stamp.SetFullCompression();
                    stamp.Close();

                    return get_file_size(filename);
                }
            }
            catch (Exception e) {
                Console.Write(e);
                Console.WriteLine();
                Console.WriteLine();
                return file.Length;
            }
        }

        static bool optimize_pdf(string src, string dest) {
            try {
                var reader = new PdfReader(src);
                var fs = new FileStream(dest, FileMode.Create);
                var stamp = new PdfStamper(reader, fs);
                reader.RemoveUnusedObjects();
                stamp.SetFullCompression();
                stamp.Close();
                return true;
            }
            catch (Exception e) {
                Console.Write(e);
                Console.WriteLine(System.Environment.NewLine);
                return false;
            }
        }

        static List<Dictionary<string, string>> getPDFs(string root_path)
        {
            var pdf_paths = new List<Dictionary<string, string>>();
            var src_dir = new DirectoryInfo(SRC);
            var q = new Queue<DirectoryInfo>();
            q.Enqueue(src_dir);
            while (q.Count > 0)
            {
                var cur_dir = q.Dequeue();
                foreach (var child in cur_dir.GetDirectories())
                {
                    if (child.Name != "_svn")
                    {
                        q.Enqueue(child);
                    }
                }

                foreach (var file in cur_dir.GetFiles())
                {
                    if (file.Extension.ToLower() == ".pdf")
                    {
                        var dict = new Dictionary<string, string>();                        
                        dict.Add("og", file.FullName);
                        dict.Add("bak", file.FullName + "~");
                        pdf_paths.Add(dict);
                        //SRC_PDF_USAGE += file.Length;
                        //DEST_PDF_USAGE += remove_embedded_fonts(file);
                        //DEST_PDF_USAGE += optimize_pdf(file);
                    }
                }
            }

            return pdf_paths;
        }

        static void Main(string[] args) {
            //get the pdf path to be copied
            //copy the pdf and append new name
            var SRC_PDF_USAGE = 0.0;
            var DEST_PDF_USAGE = 0.0;

            var pdf_paths = getPDFs(SRC);

            foreach(var _path in pdf_paths)
            {
                SRC_PDF_USAGE += get_file_size(_path["og"]);
            }

            //create _bak folder
            var src_dir = new DirectoryInfo(SRC);
            var bak_path = src_dir.FullName + "/pdf_bak";

            for(var i = 0; i < pdf_paths.Count; ++i)
            {
                var filename = pdf_paths[i]["og"];
                var backup = pdf_paths[i]["bak"];

                File.Copy(filename, backup, true);

                if(optimize_pdf(backup, filename))
                {
                    //remove_embedded_fonts(new FileInfo(filename));
                    DEST_PDF_USAGE += new FileInfo(filename).Length;
                }
                else
                {
                    DEST_PDF_USAGE += get_file_size(filename);
                }
            }

            Console.WriteLine("SRC pdf space usage: " + SRC_PDF_USAGE / MB + " MB");
            Console.WriteLine("DEST pdf space usage: " + DEST_PDF_USAGE / MB + " MB");
            Console.WriteLine("reduction %: " + ((1 - (DEST_PDF_USAGE / SRC_PDF_USAGE)) * 100));
            Console.Read();
        }
    }
}
