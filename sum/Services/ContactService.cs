using sum.Models;

namespace sum.Services
{
    public class ContactService
    {
        // Teacher-to-subjects mapping (deterministic, not random)
        private static readonly Dictionary<string, TeacherContact> TeacherDatabase = new Dictionary<string, TeacherContact>
        {
            {
                "Mgr. Kovářová",
                new TeacherContact
                {
                    FullName = "Mgr. Jarmila Kovářová",
                    Email = "kovarova@zschvalkovice.cz",
                    Phone = "+420 123 456 001",
                    Office = "A101",
                    Specialization = "Čeština, Literatura",
                    Subjects = new List<string> { "Český jazyk" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Úterý: 14:00 - 15:00, Čtvrtek: 14:00 - 15:00"
                }
            },
            {
                "Mgr. Novotná",
                new TeacherContact
                {
                    FullName = "Mgr. Petra Novotná",
                    Email = "novotna@zschvalkovice.cz",
                    Phone = "+420 123 456 002",
                    Office = "A102",
                    Specialization = "Čeština, Dramatika",
                    Subjects = new List<string> { "Český jazyk" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Středa: 14:00 - 15:00, Pátek: 14:00 - 15:00"
                }
            },
            {
                "Mgr. Novák",
                new TeacherContact
                {
                    FullName = "Mgr. Jan Novák",
                    Email = "novak@zschvalkovice.cz",
                    Phone = "+420 123 456 003",
                    Office = "A102",
                    Specialization = "Matematika, Fyzika",
                    Subjects = new List<string> { "Matematika" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Pondělí: 15:00 - 16:00, Pátek: 15:00 - 16:00"
                }
            },
            {
                "Mgr. Svobodová",
                new TeacherContact
                {
                    FullName = "Mgr. Jana Svobodová",
                    Email = "svobodova@zschvalkovice.cz",
                    Phone = "+420 123 456 004",
                    Office = "A103",
                    Specialization = "Matematika, Statistika",
                    Subjects = new List<string> { "Matematika" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Středa: 15:00 - 16:00"
                }
            },
            {
                "Mgr. Holubová",
                new TeacherContact
                {
                    FullName = "Mgr. Eva Holubová",
                    Email = "holubova@zschvalkovice.cz",
                    Phone = "+420 123 456 005",
                    Office = "B201",
                    Specialization = "Anglická literatura, Konverzace",
                    Subjects = new List<string> { "Anglický jazyk" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Úterý: 15:00 - 16:00, Čtvrtek: 15:00 - 16:00"
                }
            },
            {
                "Bc. Černý",
                new TeacherContact
                {
                    FullName = "Bc. David Černý",
                    Email = "cerny@zschvalkovice.cz",
                    Phone = "+420 123 456 006",
                    Office = "B202",
                    Specialization = "Anglický jazyk, Gramatikayka",
                    Subjects = new List<string> { "Anglický jazyk" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Pondělí: 14:00 - 15:00, Pátek: 14:00 - 15:00"
                }
            },
            {
                "Mgr. Bartoš",
                new TeacherContact
                {
                    FullName = "Mgr. Pavel Bartoš",
                    Email = "bartos@zschvalkovice.cz",
                    Phone = "+420 123 456 007",
                    Office = "A105",
                    Specialization = "Fyzika, Elektrotechnika",
                    Subjects = new List<string> { "Fyzika" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Středa: 14:00 - 15:00"
                }
            },
            {
                "Mgr. Dvořák",
                new TeacherContact
                {
                    FullName = "Mgr. František Dvořák",
                    Email = "dvorak@zschvalkovice.cz",
                    Phone = "+420 123 456 008",
                    Office = "A104",
                    Specialization = "Dějepis, Vlastivěda, Zeměpis",
                    Subjects = new List<string> { "Dějepis", "Vlastivěda", "Zeměpis" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Úterý: 14:00 - 15:00, Pátek: 15:00 - 16:00"
                }
            },
            {
                "Mgr. Kratochvílová",
                new TeacherContact
                {
                    FullName = "Mgr. Hana Kratochvílová",
                    Email = "kratochvilova@zschvalkovice.cz",
                    Phone = "+420 123 456 009",
                    Office = "A103",
                    Specialization = "Chemie, Přírodopis",
                    Subjects = new List<string> { "Chemie", "Přírodopis" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Pondělí: 14:00 - 15:00, Čtvrtek: 14:00 - 15:00"
                }
            },
            {
                "Ing. Svoboda",
                new TeacherContact
                {
                    FullName = "Ing. Martin Svoboda",
                    Email = "svoboda@zschvalkovice.cz",
                    Phone = "+420 123 456 010",
                    Office = "PC1",
                    Specialization = "Informatika, Programování",
                    Subjects = new List<string> { "Informatika" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Středa: 15:00 - 16:00, Pátek: 15:00 - 16:00"
                }
            },
            {
                "Mgr. Pokorná",
                new TeacherContact
                {
                    FullName = "Mgr. Michaela Pokorná",
                    Email = "pokorna@zschvalkovice.cz",
                    Phone = "+420 123 456 011",
                    Office = "B202",
                    Specialization = "Občanská výchova, Výtvarná výuka, Hudba",
                    Subjects = new List<string> { "Občanská výchova", "Výtvarná výchova", "Hudební výchova" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Čtvrtek: 14:00 - 15:00"
                }
            },
            {
                "Mgr. Vrána",
                new TeacherContact
                {
                    FullName = "Mgr. Tomáš Vrána",
                    Email = "vrana@zschvalkovice.cz",
                    Phone = "+420 123 456 012",
                    Office = "Tělocvična",
                    Specialization = "Tělesná výchova, Sportovní trénink",
                    Subjects = new List<string> { "Tělesná výchova" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Pondělí: 15:00 - 16:00"
                }
            },
            {
                "Mgr. Veselá",
                new TeacherContact
                {
                    FullName = "Mgr. Lucie Veselá",
                    Email = "vesela@zschvalkovice.cz",
                    Phone = "+420 123 456 013",
                    Office = "Tělocvična",
                    Specialization = "Tělesná výchova, Zdravotní péče",
                    Subjects = new List<string> { "Tělesná výchova" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Pátek: 14:00 - 15:00"
                }
            },
            {
                "Mgr. Malá",
                new TeacherContact
                {
                    FullName = "Mgr. Kateřina Malá",
                    Email = "mala@zschvalkovice.cz",
                    Phone = "+420 123 456 014",
                    Office = "A101",
                    Specialization = "Prvouka, Přírodověda",
                    Subjects = new List<string> { "Prvouka", "Přírodověda" },
                    Availability = "Pondělí - Pátek: 8:00 - 16:00",
                    ConsultationHours = "Středa: 14:00 - 15:00"
                }
            }
        };

        private static readonly List<AdminContact> AdminDatabase = new List<AdminContact>
        {
            new AdminContact
            {
                FullName = "Mgr. Anna Kovářová",
                Position = "Ředitelka",
                Email = "reditelka@zschvalkovice.cz",
                Phone = "+420 123 456 100",
                Office = "Kabinet ředitele",
                Expertise = "Vedení, Pedagogika",
                Availability = "Pondělí - Pátek: 8:00 - 16:00",
                ConsultationHours = "Úterý: 14:00 - 15:30, Čtvrtek: 14:00 - 15:30",
                Description = "Ředitelka školy, odpovídá za celkový chod a kvalitu vzdělávání."
            },
            new AdminContact
            {
                FullName = "Mgr. Josef Procházka",
                Position = "Zástupce ředitele",
                Email = "zastupce@zschvalkovice.cz",
                Phone = "+420 123 456 101",
                Office = "Kabinet zástupce",
                Expertise = "Správa, Personalistika",
                Availability = "Pondělí - Pátek: 8:00 - 16:00",
                ConsultationHours = "Pondělí: 14:00 - 15:00, Pátek: 14:00 - 15:00",
                Description = "Zástupce ředitele, stará se o administrativu a personálu."
            },
            new AdminContact
            {
                FullName = "Alena Nováková",
                Position = "Sekretářka",
                Email = "sekretariat@zschvalkovice.cz",
                Phone = "+420 123 456 102",
                Office = "Sekretariát",
                Expertise = "Administrativa, Agendy",
                Availability = "Pondělí - Pátek: 8:00 - 16:00",
                ConsultationHours = "Celý den",
                Description = "Obecná administrativa, podání, informace."
            },
            new AdminContact
            {
                FullName = "Jan Kučera",
                Position = "Správce budovy",
                Email = "spravce@zschvalkovice.cz",
                Phone = "+420 123 456 103",
                Office = "Hospodářství",
                Expertise = "Údržba, BOZP",
                Availability = "Pondělí - Pátek: 7:00 - 16:00",
                ConsultationHours = "Na objednávku",
                Description = "Údržba budovy, bezpečnost a ochrana zdraví."
            },
            new AdminContact
            {
                FullName = "Petra Zavřelová",
                Position = "Údržbářka",
                Email = "udrzba@zschvalkovice.cz",
                Phone = "+420 123 456 104",
                Office = "Hospodářství",
                Expertise = "Čistota, Údržba",
                Availability = "Pondělí - Pátek: 6:30 - 14:30",
                ConsultationHours = "Na objednávku",
                Description = "Údržba a čistota prostor školy."
            },
            new AdminContact
            {
                FullName = "Libuše Horákova",
                Position = "Účetní",
                Email = "ucetni@zschvalkovice.cz",
                Phone = "+420 123 456 105",
                Office = "Účtárna",
                Expertise = "Účetnictví, Finance",
                Availability = "Pondělí - Pátek: 8:00 - 16:00",
                ConsultationHours = "Úterý: 10:00 - 11:00, Čtvrtek: 10:00 - 11:00",
                Description = "Finance a účetnictví školy."
            }
        };

        public ContactsViewModel GetAllContacts()
        {
            var vm = new ContactsViewModel();

            // Add teachers in alphabetical order
            foreach (var teacher in TeacherDatabase.Values.OrderBy(t => t.FullName))
            {
                vm.Teachers.Add(teacher);
            }

            // Add administrators in position hierarchy
            vm.Administrators = AdminDatabase.OrderByDescending(a => a.Position == "Ředitelka")
                .ThenByDescending(a => a.Position == "Zástupce ředitele")
                .ThenBy(a => a.Position)
                .ToList();

            return vm;
        }

        public TeacherContact? GetTeacherByName(string teacherName)
        {
            if (TeacherDatabase.TryGetValue(teacherName, out var teacher))
            {
                return teacher;
            }
            return null;
        }

        public List<TeacherContact> GetTeachersBySubject(string subject)
        {
            return TeacherDatabase.Values
                .Where(t => t.Subjects.Contains(subject))
                .OrderBy(t => t.FullName)
                .ToList();
        }

        public List<TeacherContact> GetAllTeachers()
        {
            return TeacherDatabase.Values
                .OrderBy(t => t.FullName)
                .ToList();
        }

        public List<AdminContact> GetAllAdministrators()
        {
            return AdminDatabase
                .OrderByDescending(a => a.Position == "Ředitelka")
                .ThenByDescending(a => a.Position == "Zástupce ředitele")
                .ThenBy(a => a.Position)
                .ToList();
        }
    }
}
