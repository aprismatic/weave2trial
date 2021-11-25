using System.Collections.Generic;

namespace weave2trial
{
    public class NodeIdentity
    {
        private readonly string _id;

        public NodeIdentity() {
            _id = _names.Dequeue();
        }

        public override string ToString() => _id;
        public override int GetHashCode() => _id.GetHashCode();
        public override bool Equals(object? obj) => obj is NodeIdentity niobj && _id.Equals(niobj._id);
        public static bool operator ==(NodeIdentity? a, NodeIdentity? b) => a is null ? b is null : b is not null && a._id.Equals(b._id);
        public static bool operator !=(NodeIdentity? a, NodeIdentity? b) => !(a == b);

        private static Queue<string> _names = new(new[] {
            "Flossie", "Kittie", "Zofia", "Dawne", "Fatimah", "Renay", "Sharee", "Patria", "Chu", "Carie", "Everette",
            "Shana", "Laraine", "Marcela", "Deb", "Lael", "Bonny", "Clementine", "Rocky", "Lucretia", "Jacki", "Becky",
            "Tobi", "Tomiko", "Elmo", "Aleisha", "Ila", "Shonda", "Kyra", "Jacinda", "Marion", "Johnnie", "Claris",
            "Karey", "Kindra", "Elise", "Sherley", "Tamika", "Mirian", "Thea", "Alix", "Eloy", "Renetta", "Brock",
            "Maxwell", "Enoch", "Ona", "Erlinda", "Joslyn", "Wava", "Colby", "Vivian", "Erik", "Lindy", "Desiree",
            "Maryetta", "Tamie", "Elia", "Harlan", "Adena", "Isreal", "Glory", "Alejandrina", "Natacha", "Eleonora",
            "Loris", "Rolande", "Royce", "Brooke", "Georgianne", "Adelle", "Gregorio", "Lajuana", "Teresa", "Ellie",
            "Jacalyn", "Lisa", "Kali", "Sheridan", "Stan", "Kris", "Loyce", "Cletus", "Justin", "Norberto", "Irma",
            "Librada", "Neal", "Ngoc", "Verna", "Latia", "Lashaun", "Danny", "Marian", "Nicola", "Reagan", "Dierdre",
            "Armandina", "Truman", "Ming", "Brande", "Marylou", "Deirdre", "Dollie", "Mariam", "Jamaal", "Kent",
            "January", "Sheryl", "Jesse", "Shanda", "Vilma", "Andra", "Garrett", "Monique", "Jerlene", "Kenna",
            "Wilfredo", "Nelly", "Omega", "Jamar", "Salina", "Arlie", "Allison", "Yevette", "Cristen", "Roberta",
            "Cathern", "Annamae", "Holly", "Pamella", "Claribel", "Dominga", "Mardell", "Cyrus", "Brent", "Kacy",
            "Charlsie", "Fritz", "Kristen", "Sophie", "Lucie", "Amiee", "Ja", "Milagros", "Bart", "Reggie", "Lauretta",
            "Katheleen", "Kip", "Chana", "Melania", "Brandie", "Loida", "Hana", "Marquis", "Leanna", "Nicolas", "Graig",
            "Mohammed", "Sage", "Christena", "Lasonya", "Karma", "Almeta", "Marcelino", "Carey", "Glinda", "Sanford",
            "Nereida", "Deetta", "Ludivina", "Patrica", "Amada", "Jeanmarie", "Rodney", "Charisse", "Filiberto",
            "Johnna", "Madaline", "Genesis", "Leeann", "Emmie", "Julietta", "Carmon", "Elli", "Rozella", "Alton",
            "Charlie", "Maryalice", "Margareta", "Norine", "Ervin", "Kandra", "Lon", "Awilda"
        });
    }
}
