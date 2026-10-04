namespace Security_agent
{
    public class EmployeeDocuments
    {
        public string Passport {get; private set;} = "";
        // Наличие военного билета
        public bool MilitaryIdCard {get; private set; }
        public string EmploymentRecordBook {get; private set;} = "";
        public string EducationalDocument {get; private set;} ="";
        // Наличие судимости
        public bool CriminalRecord {get; private set;}
        // Наличие медицинского заключения
        public bool MedicalReport {get; private set;}
        public string MedicalBook {get; private set;} = "";

        // Обновление документов сотрудника
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