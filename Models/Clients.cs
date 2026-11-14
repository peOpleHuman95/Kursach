using System;
using System.Collections.Generic;
using System.Linq;

namespace Security_agent
{
    // Общие данные физических и юридических лиц
    public abstract class Client
    {
        public string Address {get; private set;} = "";
        // Проверяет и сохраняет адрес при создании клиента.
        protected Client (string address)
        {
            ChangeAddress(address);
        }
        // Изменение адреса с проверкой заполнения
        public void ChangeAddress(string newAddress)
        {
            if (string.IsNullOrWhiteSpace(newAddress)) throw new ArgumentException("Адрес не может быть пустым", nameof(newAddress));
            newAddress = newAddress.Trim();
            if (newAddress == Address) return;

            Address = newAddress;

        }
    }
    // Клиент - Юридическое лицо
    public class ClientLegalEntity : Client
    {

        public string NameFirm {get; private set;} = "";
        
        // Создаёт карточку организации с названием и адресом.
        public ClientLegalEntity(string nameFirm,string address) :base(address)
        {
           ChangeNameFirm(nameFirm);
        
        }
       // Изменение названия фирмы с проверкой заполнения
        public void ChangeNameFirm(string newNameFirm)
        {
            if (string.IsNullOrWhiteSpace(newNameFirm)) throw new ArgumentException("Название не может быть пустым", nameof(newNameFirm));
            newNameFirm = newNameFirm.Trim();
            if (newNameFirm == NameFirm) return;

            NameFirm = newNameFirm;
        }
    } 
    // Клиент - Физическое лицо
    public class ClientIndividual : Client
    {
        public string FirstName {get; private set;} = "";
        public string LastName {get; private set;} = "";
        public string MiddleName {get; private set;} = "";
        public string Passport {get; private set;} = "";

        // Создаёт карточку физического лица с адресом, ФИО и паспортными данными.
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
        // Обновление ФИО и паспортных данных клиента
        public void UpdatePersonalData(
            string firstName,
            string lastName,
            string middleName,
            string passport)
        {
            if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("Имя не должно быть пустым.", nameof(firstName));
            if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("Фамилия не должна быть пустой.", nameof(lastName));
            if (string.IsNullOrWhiteSpace(passport)) throw new ArgumentException("Паспортные данные не должны быть пустыми.", nameof(passport));

            FirstName = firstName.Trim();
            LastName = lastName.Trim();
            MiddleName = (middleName ?? "").Trim();
            Passport = passport.Trim();
        }
    }
}
