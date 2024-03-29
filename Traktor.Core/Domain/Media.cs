﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace Traktor.Core.Domain
{
    [Table("Media")]
    public abstract class Media : IEquatable<Media>, ICloneable
    {
        public class EqualityComparer<T> : IEqualityComparer<T> where T : Media
        {
            private bool useDbId = false;
            public EqualityComparer(bool useDbId = false)
            {
                this.useDbId = useDbId;
                if (typeof(T) == typeof(Media) && !this.useDbId)
                    throw new InvalidOperationException("Can't compare base class media, unless useDbId is TRUE");
            }

            public bool Equals(T x, T y)
            {
                if (useDbId)
                    return x.DbId == y.DbId;

                return x.Equals(y);
            }

            public int GetHashCode(T obj)
            {
                if (useDbId)
                    return obj.DbId.GetHashCode();

                switch (obj)
                {
                    case Episode e:
                        return (e.ShowId.Trakt, e.Season, e.Number).GetHashCode();
                    default:
                        return obj.Id.GetKey().GetHashCode();
                }
            }
        }

        [Key]
        public int DbId { get; set; }

        [Owned]
        public class MediaId : IEquatable<MediaId>
        {
            private MediaId() { }

            public MediaId(int? trakt, string slug, string imdb, int? tvdb, int? tmdb)
            {
                this.Trakt = trakt;
                this.Slug = slug;
                this.TVDB = tvdb;
                this.IMDB = imdb;
                this.TMDB = tmdb;
            }

            public int? Trakt { get; set; }
            public string Slug { get; set; }
            public int? TVDB { get; set; }
            public string IMDB { get; set; }
            public int? TMDB { get; set; }

            public bool Equals(MediaId other)
            {
                if (this.Trakt.HasValue && this.Trakt == other.Trakt)
                    return true;

                if (!string.IsNullOrEmpty(this.IMDB) && this.IMDB == other.IMDB)
                    return true;

                if (this.TVDB.HasValue && this.TVDB == other.TVDB)
                    return true;

                if (this.TMDB.HasValue && this.TMDB == other.TMDB)
                    return true;

                return false;
            }

            public string GetKey()
            {
                if (!string.IsNullOrEmpty(this.IMDB))
                    return this.IMDB;

                return (this.Trakt ?? this.TVDB ?? this.TMDB)?.ToString() ?? this.Slug;
            }

            public override string ToString()
            {
                if (!string.IsNullOrEmpty(this.IMDB))
                    return $"IMDB: {this.IMDB}";
                if (this.TVDB.HasValue)
                    return $"TVDB: {this.TVDB}";
                if (this.TMDB.HasValue)
                    return $"TMDB: {this.TMDB}";

                if (this.Trakt.HasValue)
                    return $"Trakt: {this.Trakt}";
                return $"Unknown Id";
            }

            public override int GetHashCode()
            {
                return this.GetKey().GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return this.Equals(obj as MediaId);
            }
        }

        public MediaId Id { get; set; }

        public string Title { get; set; }
        public int Year { get; set; }

        public string[] Genres { get; set; }

        public string ImageUrl { get; set; }

        public enum MediaState
        {
            Registered,
            Awaiting,
            Available,
            Collected,
            Abandoned
        }
        public MediaState State { get; private set; }
        public DateTime StateDate { get; set; }

        public DateTime? Release { get; set; }
        public DateTime? CollectedAt { get; set; }
        public DateTime? WatchlistedAt { get; set; }

        public DateTime? FirstSpottedAt { get; set; }

        public DateTime? LastScoutedAt { get; set; }

        public DateTime? WatchedAt { get; set; }

        [NotMapped]
        public IReadOnlyList<Scouter.ScoutResult.Magnet> Magnets { get; set; }

        [NotMapped]
        public List<Uri> BannedMagnets { get; set; } = new List<Uri>();

        public Uri Magnet { get; private set; }

        public string[] RelativePath { get; set; }

        public virtual bool Equals(Media other)
        {
            return this.Id.Equals(other.Id);
        }

        public void ChangeStateTo(MediaState state)
        {
            //var oldState = this.State;
            switch (state)
            {
                case MediaState.Available:
                    if ((this.Release ?? DateTime.MaxValue) > DateTime.Now)
                        throw new InvalidOperationException("Can't set state to [Available] because Release date is in the future.");
                    break;
                case MediaState.Registered:
                    this.Magnets = null;
                    this.Magnet = null;
                    break;
            }

            this.State = state;
            this.StateDate = DateTime.Now;
        }

        public void SetMagnet(Uri uri, bool force = false)
        {
            if (this.Magnet == null || force)
                this.Magnet = uri;
            else throw new InvalidOperationException("Magnet has already been set.");
        }

        public virtual void AddMagnets(IReadOnlyList<Scouter.ScoutResult.Magnet> magnets, bool forceSelect = false)
        {
            this.Magnets = magnets;

            if (this.Magnet == null || forceSelect)
            {
                int skip = 0;
                while (skip < magnets.Count)
                {
                    var magnet = magnets.Skip(skip).FirstOrDefault();
                    magnet.ConvertLink();

                    skip++;
                    if (!BannedMagnets.Contains(magnet.Link))
                    {
                        this.SetMagnet(magnet.Link, forceSelect);
                        return;
                    }
                }

                Curator.Debug($"Magnet selection ran out of viable magnets. Banned Magnet Count={BannedMagnets.Count} vs Magnet Count={magnets.Count} - clearing banned magnets.");
                BannedMagnets.Clear();
            }
        }

        public override string ToString()
        {
            return $"{Title}, {Year} ({this.Id})";
        }

        public bool IsAnime()
        {
            return this.Genres?.Any(x => x.Equals("anime", StringComparison.CurrentCultureIgnoreCase)) ?? false;
        }

        public void Reset()
        {
            this.State = MediaState.Registered;
            this.StateDate = DateTime.Now;
            this.Magnet = null;
            this.Magnets = null;
        }

        public int GetPriority()
        {
            if (this is Episode episode)
                return (100 - episode.Number - episode.Season) + Math.Max(0, 7 - (int)(DateTime.Now - (this.Release ?? this.StateDate)).TotalDays);
            return Math.Max(0, 360 - (int)(DateTime.Now - (this.Release ?? this.StateDate)).TotalDays);
        }

        public virtual string GetPhysicalName()
        {
            return $"{this.Title}{(this.Id.Slug.EndsWith(this.Year.ToString()) ? $" ({this.Year})" : "")}";
        }

        public virtual string GetCanonicalName()
        {
            return this.Title;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    public class Movie : Media
    {
        private Movie() { }

        public Movie(MediaId id)
        {
            this.Id = id;
        }

        //public DateTime? PhysicalRelease { get; set; }
    }

    public class Episode : Media, IEquatable<Episode>
    {
        private Episode() { }

        public Episode(MediaId showId, int season, int number)
        {
            this.ShowId = showId;
            this.Season = season;
            this.Number = number;
        }

        public int Number { get; protected set; }
        public int Season { get; protected set; }

        [NotMapped]
        public MediaId ShowId { get; set; } // We save this onto shadow properties when inserting

        public string ShowTitle { get; set; }

        public int TotalEpisodesInSeason { get; set; }

        public void AddMagnets(IReadOnlyList<Scouter.ScoutResult.Magnet> magnets, bool selectFullSeason = false, bool forceSelect = false)
        {
            this.Magnets = magnets;

            if (this.Magnet == null || forceSelect)
            {
                var magnet = magnets.OrderByDescending(x => x.Score).ThenByDescending(x => x.IsFullSeason == selectFullSeason).FirstOrDefault();
                magnet.ConvertLink();

                this.SetMagnet(magnet.Link, forceSelect);
            }
        }

        public override bool Equals(Media other)
        {
            return Equals(other as Episode);
        }

        public bool Equals(Episode other)
        {
            return this.ShowId.Equals(other.ShowId) && this.Season == other.Season && this.Number == other.Number;
        }

        public override string GetPhysicalName()
        {
            return $"{this.ShowTitle}{(this.ShowId.Slug.EndsWith(this.Year.ToString()) ? $" ({this.Year})" : "")}";
        }

        public override string ToString()
        {
            return $"{ShowTitle} ({Year}) - S{Season:00}E{Number:00} - {Title} ({this.Id})";
        }

        public override string GetCanonicalName()
        {
            return this.ShowTitle;
        }

        public Episode Clone(int season, int episode)
        {
            if (this.Season == season && this.Number == episode)
                throw new InvalidOperationException("Can't clone an Episode with the same season and episode number.");

            var cloned = this.MemberwiseClone() as Episode;
            cloned.Number = episode;
            cloned.Season = season;

            return cloned;
        }
    }
}
