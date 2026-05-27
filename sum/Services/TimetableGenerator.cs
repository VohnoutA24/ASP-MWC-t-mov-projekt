using System;
using System.Collections.Generic;
using sum.Models;

namespace sum.Services
{
    public static class TimetableGenerator
    {
        private static readonly string[] DayNames = { "Pondělí", "Úterý", "Středa", "Čtvrtek", "Pátek" };
        private static readonly string[] Times = { "8:00 – 8:45", "8:55 – 9:40", "9:55 – 10:40", "10:50 – 11:35", "11:45 – 12:30", "12:40 – 13:25", "13:35 – 14:20" };

        private class SubjectInfo
        {
            public string Name { get; set; }
            public string BadgeClass { get; set; }
            public string[] PossibleTeachers { get; set; }
            public string[] PossibleRooms { get; set; }
            public bool AllowDoubleLesson { get; set; }
        }

        private static readonly Dictionary<string, SubjectInfo> Subjects = new Dictionary<string, SubjectInfo>
        {
            { "Český jazyk", new SubjectInfo { Name = "Český jazyk", BadgeClass = "badge-czech", PossibleTeachers = new[] { "Mgr. Kovářová", "Mgr. Novotná" }, PossibleRooms = new[] { "A101", "A102" } } },
            { "Matematika", new SubjectInfo { Name = "Matematika", BadgeClass = "badge-math", PossibleTeachers = new[] { "Mgr. Novák", "Mgr. Svobodová" }, PossibleRooms = new[] { "A102", "A103" } } },
            { "Anglický jazyk", new SubjectInfo { Name = "Anglický jazyk", BadgeClass = "badge-english", PossibleTeachers = new[] { "Mgr. Holubová", "Bc. Černý" }, PossibleRooms = new[] { "B201", "B202" }, AllowDoubleLesson = true } },
            { "Fyzika", new SubjectInfo { Name = "Fyzika", BadgeClass = "badge-physics", PossibleTeachers = new[] { "Mgr. Bartoš" }, PossibleRooms = new[] { "A105" } } },
            { "Dějepis", new SubjectInfo { Name = "Dějepis", BadgeClass = "badge-history", PossibleTeachers = new[] { "Mgr. Dvořák" }, PossibleRooms = new[] { "A104" } } },
            { "Chemie", new SubjectInfo { Name = "Chemie", BadgeClass = "badge-chemistry", PossibleTeachers = new[] { "Mgr. Kratochvílová" }, PossibleRooms = new[] { "A103" } } },
            { "Zeměpis", new SubjectInfo { Name = "Zeměpis", BadgeClass = "badge-geography", PossibleTeachers = new[] { "Mgr. Dvořák" }, PossibleRooms = new[] { "A104" } } },
            { "Informatika", new SubjectInfo { Name = "Informatika", BadgeClass = "badge-it", PossibleTeachers = new[] { "Ing. Svoboda" }, PossibleRooms = new[] { "PC1", "PC2" } } },
            { "Občanská výchova", new SubjectInfo { Name = "Občanská výchova", BadgeClass = "badge-civics", PossibleTeachers = new[] { "Mgr. Pokorná" }, PossibleRooms = new[] { "B202" } } },
            { "Přírodopis", new SubjectInfo { Name = "Přírodopis", BadgeClass = "badge-biology", PossibleTeachers = new[] { "Mgr. Kratochvílová" }, PossibleRooms = new[] { "A103" } } },
            { "Tělesná výchova", new SubjectInfo { Name = "Tělesná výchova", BadgeClass = "badge-pe", PossibleTeachers = new[] { "Mgr. Vrána", "Mgr. Veselá" }, PossibleRooms = new[] { "Tělocvična" }, AllowDoubleLesson = true } },
            { "Výtvarná výchova", new SubjectInfo { Name = "Výtvarná výchova", BadgeClass = "badge-art", PossibleTeachers = new[] { "Mgr. Pokorná" }, PossibleRooms = new[] { "B203" }, AllowDoubleLesson = true } },
            { "Hudební výchova", new SubjectInfo { Name = "Hudební výchova", BadgeClass = "badge-music", PossibleTeachers = new[] { "Mgr. Pokorná" }, PossibleRooms = new[] { "B202" } } },
            { "Prvouka", new SubjectInfo { Name = "Prvouka", BadgeClass = "badge-biology", PossibleTeachers = new[] { "Mgr. Malá" }, PossibleRooms = new[] { "A101" } } },
            { "Přírodověda", new SubjectInfo { Name = "Přírodověda", BadgeClass = "badge-biology", PossibleTeachers = new[] { "Mgr. Malá" }, PossibleRooms = new[] { "A103" } } },
            { "Vlastivěda", new SubjectInfo { Name = "Vlastivěda", BadgeClass = "badge-geography", PossibleTeachers = new[] { "Mgr. Dvořák" }, PossibleRooms = new[] { "A104" } } }
        };

        public static TimetableViewModel GenerateForGrade(int grade)
        {
            var vm = new TimetableViewModel { CurrentGrade = grade };
            for (int i = 1; i <= 9; i++) vm.AvailableGrades.Add(i);

            // Deterministic random based on grade
            var rng = new Random(grade * 12345);

            var subjectsForGrade = GetSubjectsForGrade(grade);

            for (int day = 0; day < 5; day++)
            {
                var daySchedule = new DaySchedule { DayIndex = day, DayName = DayNames[day] };
                int lessonsCount = grade <= 3 ? rng.Next(4, 5) : grade <= 5 ? rng.Next(5, 6) : rng.Next(5, 7);

                for (int period = 1; period <= lessonsCount; period++)
                {
                    // Random subject
                    string subjectKey = subjectsForGrade[rng.Next(subjectsForGrade.Count)];
                    var info = Subjects[subjectKey];

                    var lesson = new Lesson
                    {
                        PeriodNumber = period,
                        TimeRange = Times[period - 1],
                        Subject = info.Name,
                        SubjectBadgeClass = info.BadgeClass,
                        Teacher = info.PossibleTeachers[rng.Next(info.PossibleTeachers.Length)],
                        Room = info.PossibleRooms[rng.Next(info.PossibleRooms.Length)]
                    };

                    daySchedule.Lessons.Add(lesson);
                }

                // Chance for a double lesson (if applicable and enough periods)
                if (lessonsCount >= 4 && rng.Next(100) < 30) // 30% chance per day
                {
                    // Find a lesson that allows double
                    for (int p = 0; p < daySchedule.Lessons.Count - 1; p++)
                    {
                        var info = Subjects[daySchedule.Lessons[p].Subject];
                        if (info.AllowDoubleLesson)
                        {
                            // Make the next lesson the same
                            var nextLesson = daySchedule.Lessons[p + 1];
                            nextLesson.Subject = info.Name;
                            nextLesson.SubjectBadgeClass = info.BadgeClass;
                            nextLesson.Teacher = daySchedule.Lessons[p].Teacher;
                            nextLesson.Room = daySchedule.Lessons[p].Room;

                            daySchedule.Lessons[p].IsDoubleLessonTop = true;
                            nextLesson.IsDoubleLessonBottom = true;
                            break; // Only one double lesson per day max
                        }
                    }
                }

                vm.Days.Add(daySchedule);
            }

            return vm;
        }

        private static List<string> GetSubjectsForGrade(int grade)
        {
            var list = new List<string> { "Český jazyk", "Matematika", "Tělesná výchova", "Výtvarná výchova", "Hudební výchova" };

            if (grade <= 3)
            {
                list.Add("Prvouka");
            }
            else if (grade <= 5)
            {
                list.Add("Přírodověda");
                list.Add("Vlastivěda");
                list.Add("Anglický jazyk");
                list.Add("Informatika");
            }
            else
            {
                list.Add("Anglický jazyk");
                list.Add("Fyzika");
                list.Add("Dějepis");
                list.Add("Zeměpis");
                list.Add("Informatika");
                list.Add("Občanská výchova");
                list.Add("Přírodopis");
                if (grade >= 8) list.Add("Chemie");
            }

            // Duplicate core subjects to increase their probability
            list.Add("Český jazyk");
            list.Add("Matematika");
            if (grade >= 4) list.Add("Anglický jazyk");

            return list;
        }
    }
}
