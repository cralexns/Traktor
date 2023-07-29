using System;
using System.Collections.Generic;
using System.Text;
using Traktor.Core.Domain;
using static Traktor.Core.Services.Indexer.IndexerBase;

namespace Traktor.Core.Services.Indexer
{
    public interface IIndexer
    {
        string Name { get; }
        List<IndexerResult> FindResultsFor(Media media);
        Type[] SupportedMediaTypes { get; }
        string[] SpecializedGenres { get; }

        int Priority { get; }

        (int? Season, int? Episode, int? Range) GetNumbering(string title);
        IndexerResult.VideoQualityLevel GetQualityLevel(string title);
        IndexerResult.QualityTrait[] GetTraits(string title);
        string GetGroup(string title);
        string GetName(string title);
    }
}
