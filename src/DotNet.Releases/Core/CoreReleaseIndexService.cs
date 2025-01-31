﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;

namespace DotNet.Releases;

internal sealed class CoreReleaseIndexService : ICoreReleaseIndexService
{
    const string NetCoreKey = nameof(NetCoreKey);

    readonly IMemoryCache _cache;

    public CoreReleaseIndexService(IMemoryCache cache) => _cache = cache;

    Task<ReadOnlyDictionary<Product, IReadOnlyCollection<ProductRelease>>?>
        ICoreReleaseIndexService.GetReleasesAsync() =>
        _cache.GetOrCreateAsync(
            NetCoreKey,
            async entry =>
            {
                ProductCollection products = await ProductCollection.GetAsync();

                var map = new ConcurrentDictionary<Product, IReadOnlyCollection<ProductRelease>>();
                await Parallel.ForEachAsync(products, async (product, token) =>
                {
                    map[product] = await product.GetReleasesAsync();
                });

                return map.AsReadOnly();
            });
}
