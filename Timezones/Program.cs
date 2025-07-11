using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

class Program
{
    static void Main()
    {
        int year = DateTime.Now.Year;
        var timezones = TimeZoneInfo.GetSystemTimeZones();

        var results = timezones.Select(tz =>
        {
            var adjustment = tz.GetAdjustmentRules()
                .FirstOrDefault(rule => rule.DateStart.Year <= year && rule.DateEnd.Year >= year);

            bool supportsDst = tz.SupportsDaylightSavingTime && adjustment != null;

            var daylightStart = supportsDst ? adjustment.DaylightTransitionStart : default;
            var daylightEnd = supportsDst ? adjustment.DaylightTransitionEnd : default;

            return new
            {
                Id = tz.Id,
                DisplayName = tz.DisplayName,
                StandardName = tz.StandardName,
                DaylightName = tz.DaylightName,
                SupportsDaylightSavingTime = tz.SupportsDaylightSavingTime,
                DaylightSavingTime = supportsDst,
                DSTOffset = supportsDst ? (int)adjustment.DaylightDelta.TotalMinutes : 0,
                UTCOffset = -(int)tz.BaseUtcOffset.TotalMinutes,
                DaylightTransitionStart = supportsDst ? new TransitionInfo(year, daylightStart) : null,
                DaylightTransitionEnd = supportsDst ? new TransitionInfo(year, daylightEnd) : null
            };
        }).ToList();

        // Add a dummy timezone entry with a fixed rule
        results.Add(new
        {
            Id = "Dummy Fixed Rule Timezone",
            DisplayName = "(UTC+01:00) Dummy Fixed Rule",
            StandardName = "Dummy Standard Time",
            DaylightName = "Dummy Daylight Time",
            SupportsDaylightSavingTime = true,
            DaylightSavingTime = true,
            DSTOffset = 60,
            UTCOffset = -60,
            DaylightTransitionStart = new TransitionInfo(year,
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    new DateTime(1, 1, 1, 2, 0, 0), // 2:00 AM
                    3, // March
                    15)), // 15th day
            DaylightTransitionEnd = new TransitionInfo(year,
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    new DateTime(1, 1, 1, 3, 0, 0), // 3:00 AM
                    10, // October
                    25)) // 25th day
        });

        results.Add(new
        {
            Id = "Dummy 2 Fixed Rule Timezone",
            DisplayName = "(UTC+01:00) Dummy 2 Fixed Rule",
            StandardName = "Dummy Standard Time",
            DaylightName = "Dummy Daylight Time",
            SupportsDaylightSavingTime = true,
            DaylightSavingTime = true,
            DSTOffset = 60,
            UTCOffset = -60,
            DaylightTransitionStart = new TransitionInfo(year,
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    new DateTime(1, 1, 1, 0, 0, 0), // Midnight (00:00:00)
                    3, // March
                    15)), // 15th day
            DaylightTransitionEnd = new TransitionInfo(year,
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    // 24:00:00 is not valid in DateTime, so we use 00:00:00 of the next day
                    new DateTime(1, 1, 1, 23, 59, 59), // Midnight (00:00:00)
                    10, // October
                    25)) // 26th day (representing 25th at 24:00:00)
        });

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        string jsonOutput = JsonSerializer.Serialize(results, jsonOptions);
        Console.WriteLine(jsonOutput);
    }

    static DateTime GetTransitionDate(int year, TimeZoneInfo.TransitionTime transition)
    {
        if (transition.IsFixedDateRule)
        {
            return new DateTime(year, transition.Month, transition.Day,
                transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, transition.TimeOfDay.Second);
        }
        else
        {
            // Special handling for "last occurrence" (Week = 5)
            if (transition.Week == 5)
            {
                // Find the last day of the month
                int lastDayOfMonth = DateTime.DaysInMonth(year, transition.Month);
                DateTime lastDay = new DateTime(year, transition.Month, lastDayOfMonth);

                // Find the last occurrence of the specified day of week
                int targetDow = (int)transition.DayOfWeek;
                int lastDayDow = (int)lastDay.DayOfWeek;

                // Convert Sunday from 0 to 7 to match Monday=1..Sunday=7
                if (targetDow == 0) targetDow = 7;
                if (lastDayDow == 0) lastDayDow = 7;

                // Calculate days to subtract to get the last occurrence of target day
                int daysToSubtract = (lastDayDow - targetDow + 7) % 7;
                DateTime result = lastDay.AddDays(-daysToSubtract);

                return new DateTime(result.Year, result.Month, result.Day,
                    transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, transition.TimeOfDay.Second);
            }
            else
            {
                DateTime firstDayOfMonth = new DateTime(year, transition.Month, 1);

                int firstDayDow = (int)firstDayOfMonth.DayOfWeek;
                int targetDow = (int)transition.DayOfWeek;

                // Convert Sunday from 0 to 7 to match Monday=1..Sunday=7
                if (targetDow == 0) targetDow = 7;
                if (firstDayDow == 0) firstDayDow = 7;

                int firstTargetDay = 1 + ((targetDow - firstDayDow + 7) % 7);

                int day = firstTargetDay + 7 * (transition.Week - 1);

                return new DateTime(year, transition.Month, day,
                    transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, transition.TimeOfDay.Second);
            }
        }
    }

    class TransitionInfo
    {
        public string DateTime { get; }
        public int? Day { get; }
        public int Month { get; }
        public int Week { get; }
        public string TimeOfDay { get; }
        public bool IsFixedDateRule { get; }
        //public int DSTOffset { get; } // 60 -> pomicanje za ljetno vrijeme
        //public int UTCOffset { get; } // -60 -> minute od londonskog vremena (zimsko računanje)

        public TransitionInfo(int year, TimeZoneInfo.TransitionTime transition)
        {
            DateTime dt = GetTransitionDate(year, transition);

            // Adjust display for transition times at 23:59:59 to show the next day at 00:00:00
            if (transition.TimeOfDay.Hour == 23 &&
                transition.TimeOfDay.Minute == 59 &&
                transition.TimeOfDay.Second == 59)
            {
                dt = dt.AddSeconds(1); // Add 1 second to get to the next day at 00:00:00
                DateTime = dt.ToString("yyyy-MM-ddTHH:mm:ss");
                TimeOfDay = "00:00:00"; // Display as midnight of next day
            }
            else
            {
                DateTime = dt.ToString("yyyy-MM-ddTHH:mm:ss");
                TimeOfDay = transition.TimeOfDay.ToString(@"HH\:mm\:ss");
            }

            // Convert the Day of Week to our 1-7 scale (Monday=1...Sunday=7)
            int dayOfWeek = ((int)dt.DayOfWeek == 0) ? 7 : (int)dt.DayOfWeek;

            // Fixed date rules use the actual day number
            if (transition.IsFixedDateRule)
            {
                Day = dt.Day;
            }
            else
            {
                // For floating rules, use the day of week from the calculated date
                Day = dayOfWeek;
            }

            Month = dt.Month;

            // Determine the actual week of the month this date falls on
            if (!transition.IsFixedDateRule)
            {
                // Calculate which occurrence this is of the day of week in this month
                int dayOfMonth = dt.Day;
                Week = (dayOfMonth - 1) / 7 + 1;
            }
            else
            {
                Week = 0;
            }

            IsFixedDateRule = transition.IsFixedDateRule;
        }
    }
}
