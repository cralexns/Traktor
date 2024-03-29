﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Traktor.Core.Domain;
using Traktor.Core.Extensions;

namespace Traktor.Core.Services.Indexer
{
    public abstract class IndexerBase : IIndexer
    {
        public class IndexerException : Exception
        {
            public IndexerBase Indexer { get; set; }

            public IndexerException(IndexerBase indexer, string message) : base(message)
            {
                this.Indexer = indexer;
            }
        }

        public class IndexerSettings
        {
            public string ApiUrl { get; set; }
            public string ApiKey { get; set; }
            public bool Enabled { get; set; }
            public int Priority { get; set; }
        }

        public string Name { get; private set; }
        public Type[] SupportedMediaTypes { get; private set; }

        public string[] SpecializedGenres { get; private set; }

        public int Priority { get; private set; }

        public string ApiUrl { get; private set; }

        public IndexerBase(string name, Type[] supportedMediaTypes, string[] specializedGenres, IndexerSettings settings)
        {
            this.Name = name;
            this.SupportedMediaTypes = supportedMediaTypes;
            this.SpecializedGenres = specializedGenres;

            //if (settings.IndexerType != this.GetType())
            //    throw new InvalidOperationException($"Wrong IndexerSettings supplied to indexer: settings type = {settings.IndexerType} vs indexer type = {this.GetType()}");

            this.ApiUrl = settings.ApiUrl;
            this.Priority = settings.Priority;
        }

        public virtual Regex QualityRegex { get; } = new Regex(@"(?:\.|\s)(?<quality>[0-9]{3,4}p)(?:\.|\s|$)", RegexOptions.ExplicitCapture);
        public virtual Regex NumberingRegex { get; } = new Regex(@"(?:\.|\s)S(?<season>[0-9]{1,2})(?:E(?<episode>[0-9]{1,2}))?(\?-E(?<range>\d{2}))?(?:\.|\s)", RegexOptions.ExplicitCapture & RegexOptions.IgnoreCase);
        public virtual Regex TraitRegex { get; } = new Regex(@"(?:\.|\s)(?<trait>(BluRay)|(DTS-HD\.MA)|(DTS-HD)|(DTS)|(Atmos)|((?:[A-Z]*)5\.1)|(7\.1)|(AAC)|(WEB-DL)|(REPACK)|(PROPER))+", RegexOptions.ExplicitCapture);
        public virtual Regex GroupRegex { get; } = new Regex(@"(?:-)(?<group>\w*)(?:[^\.])*$", RegexOptions.ExplicitCapture);
        public virtual Regex NameRegex { get; } = new Regex(@"^(?<name>[\w\-\s\._]+?)(\d{4}|(S\d{2}))", RegexOptions.ExplicitCapture);

        public IndexerResult.VideoQualityLevel GetQualityLevel(string title)
        {
            switch (QualityRegex.Match(title).Groups["quality"]?.Value)
            {
                case "720p":
                case "x720":
                    return IndexerResult.VideoQualityLevel.HD_720p;
                case "1080p":
                case "x1080":
                    return IndexerResult.VideoQualityLevel.FHD_1080p;
                case "2160p":
                case "x2160":
                    return IndexerResult.VideoQualityLevel.UHD_2160p;
            }
            return IndexerResult.VideoQualityLevel.Unknown;
        }

        public (int? Season, int? Episode, int? Range) GetNumbering(string title)
        {
            var matches = NumberingRegex.Match(title);
            if (matches.Success)
            {
                var range = matches.Groups["range"].Value;
                if (range.Contains("-") || range.Contains("~"))
                {
                    var rangeNumbers = range.Split(new[] { '-', '~' }, StringSplitOptions.RemoveEmptyEntries);
                    if (rangeNumbers.Count() >= 2)
                        return (matches.Groups["season"].Value.ToInt(), rangeNumbers[0].ToInt(), rangeNumbers[1].ToInt());
                }

                return (matches.Groups["season"].Value.ToInt(), matches.Groups["episode"].Value.ToInt(), matches.Groups["range"].Value.ToInt());
            }
            return (null, null, null);
        }

        public IndexerResult.QualityTrait[] GetTraits(string title)
        {
            var traits = new List<IndexerResult.QualityTrait>();
            var matches = TraitRegex.Matches(title).Select(x=>x.Groups["trait"].Value.ToLower());
            foreach (var capture in matches)
            {
                switch (capture)
                {
                    case "bluray":
                        traits.Add(IndexerResult.QualityTrait.BluRay);
                        break;
                    case "dts-hd.ma":
                        traits.Add(IndexerResult.QualityTrait.DTS_HD_MA);
                        break;
                    case "dts_hd":
                        traits.Add(IndexerResult.QualityTrait.DTS_HD);
                        break;
                    case "dts":
                        traits.Add(IndexerResult.QualityTrait.DTS);
                        break;
                    case "atmos":
                        traits.Add(IndexerResult.QualityTrait.Atmos);
                        break;
                    case var a when a.EndsWith("5.1"):
                        traits.Add(IndexerResult.QualityTrait.AC5_1);
                        break;
                    case var a when a.EndsWith("7.1"):
                        traits.Add(IndexerResult.QualityTrait.AC7_1);
                        break;
                    case "aac":
                        traits.Add(IndexerResult.QualityTrait.AAC);
                        break;
                    case "web-dl":
                        traits.Add(IndexerResult.QualityTrait.WEB_DL);
                        break;
                    case "proper":
                        traits.Add(IndexerResult.QualityTrait.PROPER);
                        break;
                    case "repack":
                        traits.Add(IndexerResult.QualityTrait.REPACK);
                        break;
                }
            }
            return traits.ToArray();
        }

        public string GetGroup(string title)
        {
            return GroupRegex.Match(title).Groups["group"].Value;
        }

        public string GetName(string title)
        {
            var name = NameRegex.Match(title).Groups["name"].Value;

            if (!name.Contains(' ') && name.Contains('.'))
                return name.Replace('.', ' ').Trim();
            return name.Trim();
        }

        public List<IndexerResult> FindResultsFor(Media media)
        {
            throw new NotImplementedException();
        }
    }
}
