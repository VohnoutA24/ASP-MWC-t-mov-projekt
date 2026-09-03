namespace sum.Models
{
    public class ContactsViewModel
    {
        public List<TeacherContact> Teachers { get; set; } = new List<TeacherContact>();
        public List<AdminContact> Administrators { get; set; } = new List<AdminContact>();
    }
}
