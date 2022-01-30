// -----------------------------------------------------------------------
// <copyright file="HashRingTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Proto.Router.Tests
{
    public class HashRingTests
    {
        [Theory, InlineData(2, 1), InlineData(10, 5), InlineData(100, 10)]
        public void Can_provide_consistent_results_when_removing_values(int nodeCount, int removeCount)
        {
            var values = Enumerable.Range(0, nodeCount).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
            var hashRing = new HashRing<string>(values, value => value, MurmurHash2.Hash, 20);

            var results = new Dictionary<string, string>();

            for (var i = 0; i < 10; i++)
            {
                var key = Guid.NewGuid().ToString("N");
                var result = hashRing.GetNode(key);
                results.Add(key, result);
            }

            var removed = results.Values.Take(removeCount).ToHashSet();

            hashRing.Remove(removed);

            foreach (var (key, prevResult) in results)
            {
                var currentResult = hashRing.GetNode(key);

                if (removed.Contains(prevResult)) currentResult.Should().NotBe(prevResult);
                else currentResult.Should().Be(prevResult);
            }
        }

        [Theory, InlineData(10, 1, .8), InlineData(100, 5, .9)]
        public void Can_provide_relatively_consistent_results_when_adding_values(int nodeCount, int addedCount, double expectedRetainedRatio)
        {
            var values = Enumerable.Range(0, nodeCount).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
            var hashRing = new HashRing<string>(values, value => value, MurmurHash2.Hash, 20);

            var results = new Dictionary<string, string>();

            for (var i = 0; i < 1000; i++)
            {
                var key = Guid.NewGuid().ToString("N");
                results.Add(key, hashRing.GetNode(key));
            }

            hashRing.Add(Enumerable.Range(0, addedCount).Select(_ => Guid.NewGuid().ToString("N")).ToArray());

            double retained = results.Count(tuple => {
                    var (key, previousResult) = tuple;
                    var currentResult = hashRing.GetNode(key);
                    return previousResult.Equals(currentResult, StringComparison.Ordinal);
                }
            );

            var retainedRatio = retained / results.Count;
            retainedRatio.Should().BeLessThan(1d, "New nodes should affect the result");
            retainedRatio.Should().BeGreaterOrEqualTo(expectedRetainedRatio);
        }

        [Theory, InlineData(2, 1), InlineData(10, 10), InlineData(100, 10)]
        public void Adding_values_is_equivalent_to_ctor_values(int nodeCount, int addedCount)
        {
            var values = Enumerable.Range(0, nodeCount).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
            var added = Enumerable.Range(0, addedCount).Select(_ => Guid.NewGuid().ToString("N")).ToArray();

            var hashRing = new HashRing<string>(values.Concat(added), value => value, MurmurHash2.Hash, 20);
            var mutatedHashSet = new HashRing<string>(values, value => value, MurmurHash2.Hash, 20);
            mutatedHashSet.Add(added);

            for (var i = 0; i < 100; i++)
            {
                var key = Guid.NewGuid().ToString("N");
                var result = mutatedHashSet.GetNode(key);
                var result2 = hashRing.GetNode(key);
                result.Should().Be(result2);
            }
        }

        [Theory, InlineData(2, 1), InlineData(10, 10), InlineData(100, 10)]
        public void Removing_values_is_equivalent_to_ctor_values(int nodeCount, int removedCount)
        {
            var values = Enumerable.Range(0, nodeCount).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
            var removed = values.Take(removedCount).ToList();

            var hashRing = new HashRing<string>(values.Except(removed), value => value, MurmurHash2.Hash, 20);
            var mutatedHashSet = new HashRing<string>(values, value => value, MurmurHash2.Hash, 20);
            mutatedHashSet.Remove(removed.ToHashSet());

            for (var i = 0; i < 100; i++)
            {
                var key = Guid.NewGuid().ToString("N");
                var result = mutatedHashSet.GetNode(key);
                var result2 = hashRing.GetNode(key);
                result.Should().Be(result2);
            }
        }
    }
}