using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Traktor.Core.Extensions;

namespace Traktor.Core.Services.Indexer
{
    public class IndexerResult
    {
        //public static Regex QualityRegex { get; } = new Regex(@"(?:\.|\s)(?<quality>[0-9]{3,4}p)(?:\.|\s)", RegexOptions.ExplicitCapture);
        //public static Regex NumberingRegex { get; } = new Regex(@"(?:\.|\s)S(?<season>[0-9]{1,2})(?:E)*(?<episode>[0-9]{1,2})*(?:\.|\s)", RegexOptions.ExplicitCapture & RegexOptions.IgnoreCase);
        //public static Regex TraitRegex { get; } = new Regex(@"(?:\.|\s)(?<trait>(BluRay)|(DTS-HD\.MA)|(DTS-HD)|(DTS)|(Atmos)|((?:[A-Z]*)5\.1)|(7\.1)|(AAC)|(WEB-DL)|(REPACK)|(PROPER))+", RegexOptions.ExplicitCapture);
        //public static Regex GroupRegex { get; } = new Regex(@"(?:-)(?<group>\w*)(?:[^\.])*$", RegexOptions.ExplicitCapture);
        //public static Regex NameRegex { get; } = new Regex(@"^(?<name>[\w\-\s\._]+?)([0-9]{4})", RegexOptions.ExplicitCapture);

        //public IndexerResult(string title, string magnet)
        //{
        //    this.Title = title;
        //    this.Magnet = magnet;

        //    if (VideoQuality == VideoQualityLevel.Unknown)
        //    {
        //        switch (QualityRegex.Match(title).Groups["quality"]?.Value)
        //        {
        //            case "720p":
        //                this.VideoQuality = VideoQualityLevel.HD_720p;
        //                break;
        //            case "1080p":
        //                this.VideoQuality = VideoQualityLevel.FHD_1080p;
        //                break;
        //            case "2160p":
        //                this.VideoQuality = VideoQualityLevel.UHD_2160p;
        //                break;
        //        }
        //    }
        //    if (!Season.HasValue && !Episode.HasValue)
        //    {
        //        var matches = NumberingRegex.Match(title);
        //        if (matches.Success)
        //        {
        //            this.Season = matches.Groups["season"].Value.ToInt();
        //            this.Episode = matches.Groups["episode"].Value.ToInt();
        //        }
        //    }
        //    if (Traits == null)
        //    {
        //        var matches = TraitRegex.Match(title);
        //        if (matches.Success)
        //        {
        //            var traits = new List<QualityTrait>();
        //            foreach (var capture in matches.Groups["trait"].Captures)
        //            {
        //                switch (capture)
        //                {
        //                    case "BluRay":
        //                        traits.Add(QualityTrait.BluRay);
        //                        break;
        //                    case "DTS-HD.MA":
        //                        traits.Add(QualityTrait.DTS_HD_MA);
        //                        break;
        //                    case "DTS_HD":
        //                        traits.Add(QualityTrait.DTS_HD);
        //                        break;
        //                    case "DTS":
        //                        traits.Add(QualityTrait.DTS);
        //                        break;
        //                    case "Atmos":
        //                        traits.Add(QualityTrait.Atmos);
        //                        break;
        //                    case "5.1":
        //                        traits.Add(QualityTrait.AC5_1);
        //                        break;
        //                    case "7.1":
        //                        traits.Add(QualityTrait.AC7_1);
        //                        break;
        //                    case "AAC":
        //                        traits.Add(QualityTrait.AAC);
        //                        break;
        //                    case "WEB-DL":
        //                        traits.Add(QualityTrait.WEB_DL);
        //                        break;
        //                    case "PROPER":
        //                        this.IsProper = true;
        //                        break;
        //                    case "REPACK":
        //                        this.IsRepack = true;
        //                        break;
        //                }
        //            }
        //            this.Traits = traits.ToArray();
        //        }
        //        else this.Traits = new QualityTrait[0];
        //    }
        //    if (string.IsNullOrEmpty(Group))
        //    {
        //        this.Group = GroupRegex.Match(title).Groups["group"].Value;
        //    }
        //    if (string.IsNullOrEmpty(Name))
        //    {
        //        this.Name = NameRegex.Match(title).Groups["name"].Value;
        //    }
        //}

        public IndexerResult(IIndexer source, string title, string magnet, int seeds, int peers)
        {
            this.Title = title;
            this.Magnet = magnet;
            this.Name = source.GetName(title);
            this.VideoQuality = source.GetQualityLevel(title);
            this.Traits = source.GetTraits(title);
            this.Group = source.GetGroup(title);

            this.Seeds = seeds;
            this.Peers = peers;
        }

        private string title;
        public string Title
        {
            get
            {
                return title;
            }
            set
            {
                title = value;

            }
        }
        public string Magnet { get; private set; }

        public string Name { get; private set; }

        public int Seeds { get; private set; }
        public int Peers { get; private set; }

        public long SizeBytes { get; set; }

        public string IMDB { get; set; }
        public int? Season { get; set; }
        public int? Episode { get; set; }

        public DateTime Date { get; set; }

        public enum VideoQualityLevel
        {
            Unknown = 0,
            [Description("720p")]
            HD_720p = 1,
            [Description("1080p")]
            FHD_1080p = 2,
            [Description("4K")]
            UHD_2160p = 3
        }
        public VideoQualityLevel VideoQuality { get; private set; }

        public QualityTrait[] Traits { get; private set; }

        public string Group { get; private set; }

        public bool IsRepack => Traits.Any(x => x == QualityTrait.REPACK);
        public bool IsProper => Traits.Any(x => x == QualityTrait.PROPER);

        public bool IsFullSeason => this.Season.HasValue && !this.Episode.HasValue;

        public enum QualityTrait
        {
            [Description("AAC2.0")]
            AAC,
            [Description("Blu-ray")]
            BluRay,
            [Description("DTS")]
            DTS,
            [Description("DTS-HD")]
            DTS_HD,
            [Description("DTS-HD Master Audio")]
            DTS_HD_MA,
            [Description("AC 7.1")]
            AC7_1,
            [Description("AC 5.1")]
            AC5_1,
            [Description("Dolby Atmos")]
            Atmos,
            [Description("WebDL")]
            WEB_DL,
            [Description("PROPER")]
            PROPER,
            [Description("REPACK")]
            REPACK
        }

        public string Source { get; set; }

        public IndexerResult CloneAs(int number)
        {
            var cloned = this.MemberwiseClone() as IndexerResult;
            cloned.Episode = number;

            return cloned;
        }

        public static string GetEnumDescription(Enum value)
        {
            // Get the Description attribute value for the enum value
            FieldInfo fi = value.GetType().GetField(value.ToString());
            DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes.Length > 0)
                return attributes[0].Description;
            else
                return value.ToString();
        }
    }
}
