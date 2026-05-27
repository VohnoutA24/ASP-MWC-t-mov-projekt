using System.Collections.Generic;

namespace sum.Models
{
    public class TimetableViewModel
    {
        public int CurrentGrade { get; set; }
        public List<int> AvailableGrades { get; set; } = new List<int>();
        public List<DaySchedule> Days { get; set; } = new List<DaySchedule>();
    }

    public class DaySchedule
    {
        public int DayIndex { get; set; } // 0 = Monday, 1 = Tuesday, etc.
        public string DayName { get; set; }
        public List<Lesson> Lessons { get; set; } = new List<Lesson>();
    }

    public class Lesson
    {
        public int PeriodNumber { get; set; }
        public string TimeRange { get; set; }
        public string Subject { get; set; }
        public string Teacher { get; set; }
        public string Room { get; set; }
        public string SubjectBadgeClass { get; set; }
        public bool IsDoubleLessonTop { get; set; }
        public bool IsDoubleLessonBottom { get; set; }
    }
}
