using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Image_Enhacements
{
    class Statistical
    {

        public static List<Double> CalculateStatistics(List<double> values)
        {
            double stddev = 0;
            double max = values.Max();
            double min = values.Min();
            double mean = values.Average();
            double sum = values.Sum();
            double n = values.Count();

            if (n > 1)
            {
                double squares = values.Sum(value => (value - mean) * (value - mean));
                stddev = Math.Sqrt(squares / n);
            }
            var statistics = new List<double>() { mean, min, max, stddev };

            return statistics;
        }

        public static List<Double> ExcludeOuliers(List<double> values, double percentile)
        {
            // Absolute frecuencies
            var absolute_frecuencies = new Dictionary<double, int>();
            foreach (var key in values)
            {
                if (absolute_frecuencies.ContainsKey(key))
                    absolute_frecuencies[key] += 1;
                else
                    absolute_frecuencies.Add(key, 1);
            }

            // Relative frecuencies
            var relative_frecuencies = new Dictionary<double, double>();
            double total = values.Count();
            foreach (KeyValuePair<double, int> item in absolute_frecuencies)
            {
                var frecuency = item.Value / total;
                relative_frecuencies[item.Key] = frecuency;
            }

            // Cumulative frecuencies
            var probabilities = new Dictionary<double, double>();
            var value_list = relative_frecuencies.Keys.ToList();
            value_list.Sort();
            double cumulativeFrecuency = 0;
            foreach (var value in value_list)
            {
                var frecuency = relative_frecuencies[value];
                cumulativeFrecuency += frecuency;
                probabilities[value] = cumulativeFrecuency;
            }

            // Exclude values using percentile
            var output_list = value_list.ToList();
            foreach (var value in value_list)
            {
                var probability = probabilities[value];
                if ((probability < percentile) | (probability > (1 - percentile)))
                {
                    output_list.Remove(value);
                }
            }
            return output_list;
        }
    }
}
