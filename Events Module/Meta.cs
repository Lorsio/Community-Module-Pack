﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blish_HUD.Content;
using Blish_HUD.Modules.Managers;
using Newtonsoft.Json;

namespace Events_Module {

    [JsonObject]
    public class Meta {

        [JsonObject]
        public struct Phase {
            public string Name     { get; set; }
            public int    Duration { get; set; }
        }

        public event EventHandler<EventArgs> OnNextRunTimeChanged;

        public static List<Meta> Events;

        public string   Name       { get; set; }
        public string   Colloquial { get; set; }
        public string   Category   { get; set; }
        public DateTime Offset     { get; set; }
        public string   Difficulty { get; set; }
        public string   Location   { get; set; }
        public string   Waypoint   { get; set; }
        public string   Wiki       { get; set; }
        public int?     Duration   { get; set; }

        [JsonProperty(PropertyName = "Alert")]
        public int? Reminder { get; set; }

        [JsonProperty(PropertyName = "Repeat")]
        public TimeSpan? RepeatInterval { get; set; }

        protected List<DateTime>          _times = new List<DateTime>();
        public    IReadOnlyList<DateTime> Times => _times;

        public Phase[] Phases { get; set; }

        private DateTime _nextTime;
        public DateTime NextTime {
            get => _nextTime;
            protected set {
                if (_nextTime == value) return;

                _nextTime = value;

                this.OnNextRunTimeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [JsonIgnore]
        public bool IsWatched = false;

        [JsonIgnore]
        protected bool HasAlerted = false;

        public string Icon { get; set; }

        public static void UpdateEventSchedules() {
            if (Events == null) return;

            var tsNow = DateTime.Now.ToLocalTime().TimeOfDay;

            foreach (var e in Events) {
                TimeSpan[] justTimes = e.Times.Select(time => time.ToLocalTime().TimeOfDay).OrderBy(time => time.TotalSeconds).ToArray();
                var nextTime = justTimes.FirstOrDefault(ts => ts.TotalSeconds >= tsNow.TotalSeconds);

                if (nextTime.Ticks == 0) // Timespan default is Ticks == 0
                    e.NextTime = DateTime.Today.AddDays(1) + justTimes[0];
                else
                    e.NextTime = DateTime.Today + nextTime;

                double timeUntil = (e.NextTime - DateTime.Now).TotalMinutes;
                if (timeUntil < (e.Reminder ?? -1) && e.IsWatched) {
                    if (!e.HasAlerted) {
                        //Modules.EventTimers.EventNotification.ShowNotification(e.Name, string.IsNullOrEmpty(e.Icon) ? GameService.Content.GetTexture("102377") : GameService.Content.GetTexture(e.Icon), $"Starts in {timeUntil.Minutes().Humanize()}", 10f);
                        e.HasAlerted = true;
                    }
                } else {
                    e.HasAlerted = false;
                }
            }
        }

        public static void Load(ContentsManager cm) {
            List<Meta> metas;

            // TODO: Ensure events file exists before trying to open it
            using (var eventsReader = new StreamReader(cm.GetFileStream("events.json"))) {
                metas = JsonConvert.DeserializeObject<List<Meta>>(eventsReader.ReadToEnd());
            }

            if (metas == null) {
                Debug.WriteLine("Could not load metas!");
                return;
            }

            var uniqueEvents = new List<Meta>();

            foreach (var meta in metas) {
                meta._times.Add(meta.Offset);

                if (meta.RepeatInterval != null && meta.RepeatInterval.Value.TotalSeconds > 0) {
                    // Subtract the repeat interval to ensure that the start time isn't included twice
                    double dailyMinutes = 60 * 24 - meta.RepeatInterval.Value.TotalMinutes;
                    var lastTime = meta.Offset;

                    while (dailyMinutes > 0) {
                        var intervalTime = lastTime.Add(meta.RepeatInterval.Value);

                        meta._times.Add(intervalTime);

                        lastTime = intervalTime;

                        dailyMinutes -= meta.RepeatInterval.Value.TotalMinutes;
                    }
                }

                var rootEvent = uniqueEvents.Find(m => m.Name == meta.Name && m.Category == meta.Category);

                if (rootEvent != null)
                    rootEvent._times.AddRange(meta.Times);
                else
                    uniqueEvents.Add(meta);
            }

            //uniqueEvents.ForEach(x => Console.WriteLine($"{x.Category}: {x.Name} will start at {String.Join(", ", x.Times)}."));

            Events = uniqueEvents;

            UpdateEventSchedules();
        }

    }

}