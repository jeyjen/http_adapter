using jeyjen.db;
using jeyjen.extension;
using System;
using System.DirectoryServices.AccountManagement;

namespace http_adapter.provider
{
    public class user_catalog
    {
        private connection _con;

        public user_catalog(string cs)
        {
            _con = new connection(jeyjen.db.provider.sqlserver, cs);
        }
        public Guid? login(string domain, string login, string password)
        {
            //Data Source=RUMSKAPT115;Integrated Security=False;Initial Catalog=sds;User ID=writer;Password=Zikkurat115
            //Server=RUMSKAPT115;Database=sds;User ID=writer;Password=Zikkurat115
            var l = login.Trim().ToLower();
            var d = domain.Trim().ToLower();
            var p = password.Trim();

            var prcpl = new PrincipalContext(ContextType.Domain, d);
            Guid? id = null;
            if (prcpl.ValidateCredentials(l, p))
            {
                var r = _con.cmd("select id from users where login = :login")
                    .param("login", l)
                    .scalar<Guid?>();
                if (r.HasValue)
                {
                    id = r.Value;
                }
                else
                {
                    id = guid.generate(guid_type.at_end);
                    _con
                        .cmd("insert into users (id, domain, login) values (:id, :domain, :login)")
                        .param("id", id)
                        .param("domain", d)
                        .param("login", l)
                        .execute();
                }
            }
            return id;
        }
        public user_status status(Guid id)
        {
            var r = _con.cmd("select is_blocked from users where id=:id")
                .param("id", id)
                .retrieve();
            if (r.count > 0)
            {
                var is_blocked = r.first.get<bool>("is_blocked");
                if (is_blocked)
                {
                    return user_status.blocked;
                }
                else
                {
                    return user_status.autorized;
                }
            }
            else
            {
                return user_status.unautorized;
            }
        }
        public user detail(Guid id)
        {
            var r = _con.cmd("select id, domain, login, email, firstname, lastname, middlename where id=:id")
                .param("id", id)
                .retrieve();
            user res = null;
            if (r.count > 0)
            {
                res = new user()
                {
                    id = r.first.get<Guid>("id"),
                    domain = r.first.get<string>("domain"),
                    login = r.first.get<string>("login"),
                    email = r.first.get<string>("email"),
                    firstname = r.first.get<string>("firstname"),
                    lastname = r.first.get<string>("lastname"),
                    middlename = r.first.get<string>("middlename"),
                };
            }
            return res;
        }
        public user detail(string domain, string login)
        {
            var r = _con.cmd("select id, domain, login, email, firstname, lastname, middlename where domain=:domain, login=:login")
                .param("domain", domain)
                .param("login", login)
                .retrieve();
            user res = null;
            if (r.count > 0)
            {
                res = new user()
                {
                    id = r.first.get<Guid>("id"),
                    domain = r.first.get<string>("domain"),
                    login = r.first.get<string>("login"),
                    email = r.first.get<string>("email"),
                    firstname = r.first.get<string>("firstname"),
                    lastname = r.first.get<string>("lastname"),
                    middlename = r.first.get<string>("middlename"),
                };
            }
            return res;
        }
    }
    #region util
    public enum user_status
    {
        autorized,
        unautorized,
        blocked,
    }

    public class user
    {
        public Guid id { get; set; }
        public string domain { get; set; }
        public string login { get; set; }
        public string email { get; set; }
        public string firstname { get; set; }
        public string lastname { get; set; }
        public string middlename { get; set; }
    }
    #endregion
}
