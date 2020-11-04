﻿using System;
using System.Collections.Generic;
using System.Linq;
using PunchedCards.Helpers;

namespace PunchedCards
{
    internal static class PunchedCards
    {
        private static void Main()
        {
            var trainingData = DataHelper.ReadTrainingData().ToList();
            var testData = DataHelper.ReadTestData().ToList();

            var punchedCardBitLengths = new[] {32, 64, 128, 256, 512};
            foreach (var punchedCardBitLength in punchedCardBitLengths)
            {
                Console.WriteLine("Punched card bit length: " + punchedCardBitLength);

                var puncher = new RandomPuncher(punchedCardBitLength);
                var topPunchedCardsPerLabel =
                    GetTopPunchedCardsPerLabel(GetPunchedCardsPerLabel(trainingData, puncher), 1);

                Console.WriteLine("Unique lookup combinations per punched card (descending): " +
                                  GetPunchedCardsPerLabelString(topPunchedCardsPerLabel));

                var trainingCorrectRecognitionsPerLabel =
                    LookupHelper.CountCorrectRecognitions(trainingData, topPunchedCardsPerLabel, puncher);
                Console.WriteLine("Training results: " +
                                  trainingCorrectRecognitionsPerLabel
                                      .Sum(correctRecognitionsPerLabel => correctRecognitionsPerLabel.Value) +
                                  " correct recognitions of " + trainingData.Count);

                var testCorrectRecognitionsPerLabel =
                    LookupHelper.CountCorrectRecognitions(testData, topPunchedCardsPerLabel, puncher);
                Console.WriteLine("Test results: " +
                                  testCorrectRecognitionsPerLabel
                                      .Sum(correctRecognitionsPerLabel => correctRecognitionsPerLabel.Value) +
                                  " correct recognitions of " + testData.Count);

                Console.WriteLine();
            }

            Console.WriteLine("Press \"Enter\" to exit the program...");
            Console.ReadLine();
        }

        private static string GetPunchedCardsPerLabelString(
            IDictionary<string, IDictionary<string, IReadOnlyCollection<Tuple<string, int>>>> punchedCardsPerLabel)
        {
            var punchedCardsPerLabelUniqueLookupCounts = punchedCardsPerLabel
                .Select(punchedCardPerLabel =>
                    new Tuple<IReadOnlyCollection<int>, int>(
                        punchedCardPerLabel.Value
                            .Select(punchedCard => punchedCard.Value.Count)
                            .OrderByDescending(count => count)
                            .ToList(),
                        punchedCardPerLabel.Value.Sum(punchedCard => punchedCard.Value.Count)))
                .OrderByDescending(countsAndSum => countsAndSum.Item2)
                .ToList();
            return string.Join(", ",
                       punchedCardsPerLabelUniqueLookupCounts.Select(uniqueLookupCounts =>
                           $"{{{GetUniqueLookupsCountsString(uniqueLookupCounts)}}}")) + ": total sum " +
                   punchedCardsPerLabelUniqueLookupCounts.Sum(uniqueLookupCounts => uniqueLookupCounts.Item2);
        }

        private static string GetUniqueLookupsCountsString(Tuple<IReadOnlyCollection<int>, int> uniqueLookupCounts)
        {
            var valuesString = string.Join(", ", uniqueLookupCounts.Item1);
            return uniqueLookupCounts.Item1.Count <= 1
                ? valuesString
                : valuesString + ": sum " + uniqueLookupCounts.Item2;
        }

        private static IDictionary<string, IDictionary<string, IReadOnlyCollection<string>>> GetGlobalTopPunchedCard(
            IDictionary<string, IDictionary<string, IReadOnlyCollection<string>>> punchedCardsPerLabel)
        {
            var globalTopPunchedCard = punchedCardsPerLabel
                .OrderByDescending(punchedCardPerLabel =>
                    punchedCardPerLabel
                        .Value
                        .Sum(labelAndInputs => labelAndInputs.Value.Count))
                .First();
            return new Dictionary<string, IDictionary<string, IReadOnlyCollection<string>>>
                {{globalTopPunchedCard.Key, globalTopPunchedCard.Value}};
        }

        private static IDictionary<string, IDictionary<string, IReadOnlyCollection<Tuple<string, int>>>>
            GetTopPunchedCardsPerLabel(
                IDictionary<string, IDictionary<string, IReadOnlyCollection<Tuple<string, int>>>> punchedCardsPerLabel,
                int topPunchedCardsPerLabelCount)
        {
            var topPunchedCardsPerLabel =
                new Dictionary<string, IDictionary<string, IReadOnlyCollection<Tuple<string, int>>>>();

            for (byte i = 0; i < DataHelper.LabelCount; i++)
            {
                var label = BinaryStringsHelper.GetLabelString(i, DataHelper.LabelCount);

                var topPunchedCardsPerSpecificLabel = punchedCardsPerLabel
                    .OrderByDescending(punchedCardPerLabel => punchedCardPerLabel.Value[label].Count)
                    .Take(topPunchedCardsPerLabelCount);

                foreach (var topPunchedCardPerSpecificLabel in topPunchedCardsPerSpecificLabel)
                {
                    if (!topPunchedCardsPerLabel.TryGetValue(topPunchedCardPerSpecificLabel.Key, out var dictionary))
                    {
                        dictionary = new Dictionary<string, IReadOnlyCollection<Tuple<string, int>>>();
                        topPunchedCardsPerLabel.Add(topPunchedCardPerSpecificLabel.Key, dictionary);
                    }

                    dictionary.Add(label, topPunchedCardPerSpecificLabel.Value[label]);
                }
            }

            return topPunchedCardsPerLabel;
        }

        private static IDictionary<string, IDictionary<string, IReadOnlyCollection<Tuple<string, int>>>>
            GetPunchedCardsPerLabel(
                IEnumerable<Tuple<string, string>> trainingData,
                IPuncher<string, string, string> puncher)
        {
            return trainingData
                .SelectMany(trainingDataItem =>
                    puncher.GetInputPunches(trainingDataItem.Item1).Select(inputPunch =>
                        new Tuple<IPunchedCard<string, string>, string>(inputPunch, trainingDataItem.Item2)))
                .GroupBy(punchedCardAndLabel => punchedCardAndLabel.Item1.Key)
                .ToDictionary(
                    punchedCardByKeyGrouping => punchedCardByKeyGrouping.Key,
                    punchedCardByKeyGrouping =>
                        (IDictionary<string, IReadOnlyCollection<Tuple<string, int>>>) punchedCardByKeyGrouping
                            .GroupBy(punchedCardAndLabel => punchedCardAndLabel.Item2)
                            .ToDictionary(
                                punchedCardByLabelGrouping => punchedCardByLabelGrouping.Key,
                                punchedCardByLabelGrouping =>
                                    (IReadOnlyCollection<Tuple<string, int>>) punchedCardByLabelGrouping
                                        .Select(punchedCardAndLabel => punchedCardAndLabel.Item1.Input)
                                        .GroupBy(punchedCardInput => punchedCardInput)
                                        .Select(punchedCardInputsGrouping =>
                                            new Tuple<string, int>(punchedCardInputsGrouping.Key,
                                                punchedCardInputsGrouping.Count()))
                                        .OrderByDescending(uniqueInputAndCount => uniqueInputAndCount.Item2)
                                        //.ThenBy(uniqueInputAndCount => uniqueInputAndCount.Item1.Count(character => character == '1'))
                                        .ToList()));
        }
    }
}