using System;
using System.Collections.Generic;
using System.Linq;

namespace Security_agent
{
    
    public abstract class Client
    {
        public string Address {get; private set;} = "";
        protected Client (string address)
        {
            ChangeAddress(address);
        }
        public void ChangeAddress(string newAddress)
        {
            if (string.IsNullOrWhiteSpace(newAddress)) throw new ArgumentException("Адрес не может быть пустым", nameof(newAddress));
            newAddress = newAddress.Trim();
            if (newAddress == Address) return;

            Address = newAddress;

        }
    }
    // Юридическое лицо
    public class ClientLegalEntity : Client
    {

        public string NameFirm {get; private set;} = "";
        
        public ClientLegalEntity(string nameFirm,string address) :base(address)
        {
           ChangeNameFirm(nameFirm);
        
        }
       
        public void ChangeNameFirm(string newNameFirm)
        {
            if (string.IsNullOrWhiteSpace(newNameFirm)) throw new ArgumentException("Название не может быть пустым", nameof(newNameFirm));
            newNameFirm = newNameFirm.Trim();
            if (newNameFirm == NameFirm) return;

            NameFirm = newNameFirm;
        }
    } 
    // Физическое лицо
    public class ClientIndividual : Client
    {
        public string FirstName {get; private set;} = "";
        public string LastName {get; private set;} = "";
        public string MiddleName {get; private set;} = "";
        public string Passport {get; private set;} = "";

        public ClientIndividual(
            string firstName,
            string lastName,
            string middleName,
            string address,
            string passport)
            : base(address)
        {
            UpdatePersonalData(firstName, lastName, middleName, passport);
        }
        public void UpdatePersonalData(
            string firstName,
            string lastName,
            string middleName,
            string passport)
        {
            if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("Имя не должно быть пустым.", nameof(firstName));
            if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("Фамилия не должна быть пустой.", nameof(lastName));
            if (string.IsNullOrWhiteSpace(passport)) throw new ArgumentException("Паспортные дынные не должны быть пустыми.", nameof(passport));

            FirstName = firstName.Trim();
            LastName = lastName.Trim();
            MiddleName = (middleName ?? "").Trim();
            Passport = passport.Trim();
        }
    }

    // Один платеж
    public class Payment
    {
        public decimal Sum {get;}
        public DateTime DatePayment {get;}
        public string NumberDocument {get;}
        public Payment (decimal sum, DateTime datePayment, string numberDocument)
        {
            if (sum <= 0) throw new ArgumentException("Сумма платежей должна быть положительной.", nameof(sum));
            if (string.IsNullOrWhiteSpace(numberDocument)) throw new ArgumentException("Номер платежного документа не должен быть пустым.", nameof(numberDocument));
            Sum = sum;
            DatePayment = datePayment;
            NumberDocument = numberDocument.Trim();
        }
    }

    // Общие данные договора
    public abstract class Agreement
    {
        public Client Client {get;}
        public string NumberAgreement {get;}
        public DateTime DateAgreement {get; protected set;}
        public DateTime DateEndContract {get; protected set;}
        
        private readonly List<Payment> payments = new List<Payment>();
        public IReadOnlyList<Payment> Payments => payments.AsReadOnly();

        //Сумма всех внесенных платежей
        public decimal PaidAmount => payments.Sum(payment => payment.Sum);

        protected Agreement(
            Client client,
            string numberAgreement,
            DateTime dateAgreement,
            DateTime dateEndContract)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(numberAgreement)) throw new ArgumentException("Номер договра не может быть пустым", nameof(numberAgreement));
            if (dateEndContract < dateAgreement) throw new ArgumentException("Дата окончания не может быть раньше даты заключения.", nameof(dateEndContract));

            Client = client;
            NumberAgreement = numberAgreement.Trim();
            DateAgreement = dateAgreement;
            DateEndContract = dateEndContract;

        }
        public void AddPayment(Payment payment)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            if (payments.Contains(payment)) throw new InvalidOperationException("Этот платеж уже добавлен в договор.");
            payments.Add(payment);
        }
    }

    // Договор охраны для разового мероприятия
    public class EventAgreement : Agreement
    {
        public string EventAddress {get;}
        public DateTime DateEvent {get;}
        public EventAgreement(
            Client client,
            string numberAgreement,
            DateTime dateAgreement,
            DateTime dateEndContract,
            string eventAddress,
            DateTime dateEvent)
            :base(client, numberAgreement, dateAgreement, dateEndContract)
        {
            if (string.IsNullOrWhiteSpace(eventAddress)) throw new ArgumentException("Адрес мероприятия не может быть пустым.", nameof(eventAddress));
            
            EventAddress = eventAddress.Trim();
            DateEvent = dateEvent;
        }
    }

    // Договор охраны объекта юридического лица
    public class ObjectAgreement : Agreement
    {
        public string ObjectAddress {get;}

        public ObjectAgreement(
            ClientLegalEntity client,
            string numberAgreement,
            DateTime dateAgreement,
            DateTime dateEndContract,
            string objectAddress)
            :base(client, numberAgreement, dateAgreement, dateEndContract)
        {
            if (string.IsNullOrWhiteSpace(objectAddress)) throw new ArgumentException("Адрес объекта не может быть пустым.", nameof(objectAddress));

            ObjectAddress = objectAddress.Trim();
        }
    }
}


   