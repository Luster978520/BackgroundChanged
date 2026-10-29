using System.Collections.Generic;

namespace BackgroundChanged.Models
{
    public class StudentInfo
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public int StuId { get; set; }
    }

    public class ClassStuInfo
    {
        public string ClassName { get; set; }

        public List<StuInfo> StuList { get; set; } = new List<StuInfo>();
    }



    public class StuInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int Age { get; set; }

    }
}
