namespace Security_agent
{
    public class EmployeeDocuments
    {
        public string Passport {get; private set;} = "";
        public bool MilitaryIdCard {get; private set; }
        public string EmploymentRecordBook {get; private set;} = "";
        public string EducationalDocument {get; private set;} ="";
        public bool CriminalRecord {get; private set;}
        public bool MedicalReport {get; private set;}
        public string MedicalBook {get; private set;} = "";
        public void Update(
            string passport,
            bool hasMilitaryIdCard,
            string employeeRecordBook,
            string educationDocument,
            bool hasCriminalRecord,
            bool hasMedicalReport,
            string medicalBook
        )
        {
            Passport = passport ?? "";
            MilitaryIdCard = hasMilitaryIdCard;
            EmploymentRecordBook = employeeRecordBook ?? "";
            EducationalDocument = educationDocument ?? "";
            CriminalRecord = hasCriminalRecord;
            MedicalReport = hasMedicalReport;
            MedicalBook = medicalBook ?? "";
        }
    }
}