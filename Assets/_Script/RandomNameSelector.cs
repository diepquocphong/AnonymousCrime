using UnityEngine;
using TMPro;

public class RandomNameSelector : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public string[] names = new string[]
    {
        "John Smith", "Maria Garcia", "Zhang Wei", "Anna Müller", "Mohammed Khan",
        "Sophie Dubois", "Juan Martinez", "Wang Li", "Sarah Johnson", "Carlos Rodríguez",
        "Li Wei", "Emily Brown", "Ahmed Al-Farsi", "Ana Silva", "Kim Jong-soo",
        "Marta Gonzalez", "Alexei Petrov", "Fatima Ali", "Takashi Yamamoto", "Gabriela Costa",
        "Michael Nguyen", "Elena Ivanova", "David Garcia", "Mei Chen", "Alessandro Rossi",
        "Leila Ahmed", "Hiroshi Tanaka", "Sophie Dupont", "Pedro Ramirez", "Svetlana Ivanov",
        "Ahmed Mohamed", "Julia Santos", "Pavel Sokolov", "Carolina Fernandez", "Mohammad Ahmed",
        "Yuki Nakamura", "Sophie Lambert", "Ahmed Khan", "Sofia Rodriguez", "Ivan Petrov",
        "Mariana Oliveira", "Kenji Suzuki", "Maria Hernandez", "Dmitry Ivanov", "Isabella Rossi",
        "Ali Rahman", "Olga Volkova", "Amir Khan", "Luiz Silva", "Aya Yamamoto",
        "Patrick Murphy", "Mei Wong", "Yuri Petrov", "Antonia Garcia", "Mohammed Ali",
        "Natalia Volkova", "Tomasz Nowak", "Carmen Lopez", "Sergei Ivanov", "Nana Yamamoto",
        "Jamie Taylor", "Elena Popova", "Hasan Al-Mansoori", "Sofia Martinez", "Arjun Patel",
        "Petra Novak", "Ali Hussein", "Katya Ivanova", "Abdullah Al-Farsi", "Maria da Silva",
        "Yuri Popov", "Lea Müller", "Yusuf Mohammed", "Li Na", "Sofia Petrova",
        "Carlos Gomez", "Ayumi Tanaka", "Sergey Petrov", "Claudia Costa", "Abdul Rahman",
        "Mei Ling", "Jose Gonzalez", "Petra Novotná", "Mohammed Rahman", "Yuki Sato",
        "Elena Petrova", "Ahmed Mahmoud", "Ananya Patel", "Mikhail Ivanov", "Gabriela da Silva",
        "Amir Hussein", "Olga Ivanova", "Ali Al-Farsi", "Maria Lopez", "Dimitri Petrov",
        "Lucia Martinez", "Ahmed Hassan", "Ayumi Suzuki", "Ivan Sokolov", "Mei Yamamoto"
    };

    //void Start()
    //{
    //    SelectRandomName();
    //}

    public void SelectRandomName()
    {
        // Kiểm tra xem danh sách tên có phần tử hay không
        if (names.Length > 0)
        {
            // Chọn một tên ngẫu nhiên từ danh sách
            string randomName = names[Random.Range(0, names.Length)];

            // Thay đổi text của biến đối tượng public nameText
            if (nameText != null)
            {
                nameText.text = randomName;
            }
            else
            {
                Debug.LogWarning("Bạn chưa gán đối tượng TextMeshPro cho biến nameText.");
            }
        }
        else
        {
            Debug.LogWarning("Danh sách tên trống. Vui lòng thêm các tên vào danh sách.");
        }
    }
}
