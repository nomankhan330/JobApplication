using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Helper
{
    public static class AmountInWordsHelper
    {
        private static string[] units =
        {
            "Zero","One","Two","Three","Four","Five",
            "Six","Seven","Eight","Nine","Ten","Eleven",
            "Twelve","Thirteen","Fourteen","Fifteen",
            "Sixteen","Seventeen","Eighteen","Nineteen"
        };

        private static string[] tens =
        {
            "", "", "Twenty", "Thirty", "Forty",
            "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
        };

        public static string NumberToWords(decimal number)
        {
            long integerPart = (long)Math.Floor(number);

            int decimalPart =
                (int)((number - integerPart) * 100);

            string words =
                ConvertWholeNumber(integerPart);

            return $"{words} and {decimalPart:00}/100 Saudi Riyals Only";
        }

        private static string ConvertWholeNumber(long number)
        {
            if (number == 0)
                return "Zero";

            if (number < 20)
                return units[number];

            if (number < 100)
            {
                return tens[number / 10] +
                    ((number % 10 > 0)
                        ? " " + units[number % 10]
                        : "");
            }

            if (number < 1000)
            {
                return units[number / 100] + " Hundred" +
                    ((number % 100 > 0)
                        ? " " + ConvertWholeNumber(number % 100)
                        : "");
            }

            if (number < 1000000)
            {
                return ConvertWholeNumber(number / 1000) + " Thousand" +
                    ((number % 1000 > 0)
                        ? " " + ConvertWholeNumber(number % 1000)
                        : "");
            }

            if (number < 1000000000)
            {
                return ConvertWholeNumber(number / 1000000) + " Million" +
                    ((number % 1000000 > 0)
                        ? " " + ConvertWholeNumber(number % 1000000)
                        : "");
            }

            return number.ToString();
        }
    }
}
