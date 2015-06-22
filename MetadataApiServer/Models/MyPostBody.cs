using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MetadataApiServer.Models
{
    public class MyPostBody
    {
        public string User       { get; set; }
        public string Filename   { get; set; }
        public string BodyOfFile { get; set; }
    }
}