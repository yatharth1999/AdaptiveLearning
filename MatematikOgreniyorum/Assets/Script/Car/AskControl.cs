using System.Collections;
using System.Collections.Generic;
// using MoodMe;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AskControl : MonoBehaviour
{
    public GameObject BallonPrefab;
    
    private GameObject[] BallonSpawnPoint;
    [System.NonSerialized] public GameObject[] Ballons = new GameObject[4];

    [Space(10)]
    public TextMeshProUGUI textQuestion;
    public TextMeshProUGUI textScore;
    
    public TextMeshProUGUI textEmotionScore;
    public TextMeshProUGUI textTime;

    [System.NonSerialized] public int rightAnswer = 0;
    [System.NonSerialized] public int Score = 0;
    private int defaultTime = 60;
    [System.NonSerialized] public int time;

    private float timer = 0.0f;
    private int seconds = 0, lastSecond = 0;
    private int totalQuestionCount = 0, rightAnswerCount = 0, wrongAnswerCount = 0;

    [Space(10)]
    public GameObject menuGame;
    public GameObject menuFinish;
    public GameObject menuStart;
    public GameObject menuPause;

    private TextMeshProUGUI[] textDizi;

    [Space(10)]
    public Toggle[] Operations = new Toggle[4]; // For UI compatibility
    public TMP_Dropdown zorlukSeviyesi; // For UI compatibility
    public Sprite[] cars = new Sprite[6];
    public Image selectedCar;

    private GameObject playerCar;
    private CarMovement carMovement;

    public float difficulty = 0f; // Ranges from 0 to 1
    public int currentLevel = 1; // Maps to levels 1-10
    private int consecutiveCorrect = 0; // Tracks consecutive correct answers
    private const int correctThreshold = 3; // Threshold for level progression

    private int lastS1 = 0, lastS2 = 0, lastRandAnswer = -1;

    public RoadMove roadMove ; // Reference to the road movement script

    [Space(10)]
    public AudioSource audioSource;
    public Image[] musicImage;
    public Sprite onMusic, offMusic;

    void Start()
    {
        Time.timeScale = 0;
        time = defaultTime;

        menuStart.SetActive(true);
        menuGame.SetActive(false);
        menuFinish.SetActive(false);
        menuPause.SetActive(false);

        textDizi = menuFinish.GetComponentsInChildren<TextMeshProUGUI>();

        BallonSpawnPoint = GameObject.FindGameObjectsWithTag("ballonSpawnPoint");
        playerCar = GameObject.FindGameObjectWithTag("car");
        carMovement = playerCar.GetComponent<CarMovement>();

        playerCar.SetActive(false);

        textScore.SetText(Score.ToString());
        textEmotionScore.SetText(emotionScore.ToString());
        textTime.SetText(time.ToString());

        difficulty = 0f; // Start at level 1
        currentLevel = 1;
    }

    void Update()
    {
        timer += Time.deltaTime;
        seconds = (int)(timer % 60);
        if (seconds != lastSecond)
        {
            time--;
            textTime.SetText(time.ToString());
            lastSecond = seconds;
        }

        if (time <= 0 && Time.timeScale != 0)
        {
            EndGame();
        }
    }

    private void EndGame()
    {
        Time.timeScale = 0;
        menuStart.SetActive(false);
        menuGame.SetActive(false);
        menuFinish.SetActive(true);
        menuPause.SetActive(false);
        playerCar.SetActive(false);

        // for (int i = 0; i < Ballons.Length; i++)
        // {
        //     GameObject.Destroy(Ballons[i]);
        // }

        textDizi[1].SetText("score: " + Score.ToString());
    
        textDizi[2].SetText("Total Questions: " + totalQuestionCount.ToString());
        textDizi[3].SetText("Correct Answers: " + rightAnswerCount.ToString());
        textDizi[4].SetText("Incorrect Answers: " + wrongAnswerCount.ToString());
    }
    public void NewQuestion()
    {
        int selectOperation = GetOperationForLevel();
        int minNumber = 1, maxNumber = 10;

        // Set number ranges based on level
        switch (currentLevel)
        {
            case 1: // Addition, 1-digit
                minNumber = 1;
                maxNumber = 10;
                selectOperation = 0; // Force addition
                break;
            case 2: // Add/Sub, 1-digit
            selectOperation = Random.Range(0, 2);
            if (selectOperation == 0){
                minNumber = 10;
                maxNumber = 100;
            }  // Force subtraction
            else
            {
                minNumber = 1;
                maxNumber = 10;
            }
                
                break;
            case 3: // Add/Sub, 2-digit
                minNumber = 10;
                maxNumber = 100;
                selectOperation = Random.Range(0, 2);
                break;
            case 4: // Add/Sub/Mult, 2-digit
                selectOperation = Random.Range(0, 3);
                if (selectOperation < 2) // Add/Sub
                {
                    minNumber = 100;
                    maxNumber = 300;
                }
                else // Mult/Div
                {
                    minNumber = 1;
                    maxNumber = 20;
                }
                break;
            case 5: // All operations, 2-digit
                selectOperation = Random.Range(0, 4);
                if (selectOperation < 2) // Add/Sub
                {
                    minNumber = 100;
                    maxNumber = 300;
                }
                else // Mult/Div
                {
                    minNumber = 1;
                    maxNumber = 20;
                }
                break;
            default: // Levels 6-10
                selectOperation = Random.Range(0, 4);
                if (selectOperation < 2) // Add/Sub
                {
                    minNumber = 100;
                    maxNumber = 500;
                }
                else // Mult/Div
                {
                    minNumber = 1;
                    maxNumber = 30;
                }
                break;
        }

        // Generate valid numbers with crash prevention
        int s1 = 0, s2 = 0;
        bool validNumbers = false;
        int attempts = 0;

        while (!validNumbers && attempts < 100)
        {
            s1 = Random.Range(minNumber, maxNumber);
            s2 = Random.Range(minNumber, maxNumber);

            switch (selectOperation)
            {
                case 1: // Subtraction
                    if (s1 >= s2) validNumbers = true;
                    break;
                case 3: // Division
                    if (s2 != 0 && s1 % s2 == 0) validNumbers = true;
                    break;
                default:
                    validNumbers = true;
                    break;
            }
            attempts++;
        }

        // Fallback for division problems
        if (selectOperation == 3 && !validNumbers)
        {
            s2 = Random.Range(1, 10);
            s1 = s2 * Random.Range(1, 10);
            s1 = Mathf.Clamp(s1, minNumber, maxNumber - 1);
            s2 = Mathf.Clamp(s2, minNumber, maxNumber - 1);
        }

        lastS1 = s1;
        lastS2 = s2;

        // Set question text and correct answer
        if (selectOperation == 0)
        {
            textQuestion.SetText(s1 + " + " + s2 + " = ?");
            rightAnswer = s1 + s2;
        }
        else if (selectOperation == 1)
        {
            textQuestion.SetText(s1 + " - " + s2 + " = ?");
            rightAnswer = s1 - s2;
        }
        else if (selectOperation == 2)
        {
            textQuestion.SetText(s1 + " x " + s2 + " = ?");
            rightAnswer = s1 * s2;
        }
        else if (selectOperation == 3)
        {
            textQuestion.SetText(s1 + " / " + s2 + " = ?");
            rightAnswer = s1 / s2;
        }

        // Generate answer options
        int randAnswer = Random.Range(0, 4);
        while (lastRandAnswer == randAnswer) randAnswer = Random.Range(0, 4);
        lastRandAnswer = randAnswer;

        int[] answers = new int[4];
        int minRand = rightAnswer - 10;
        int maxRand = rightAnswer + 11; // +1 because max is exclusive in Random.Range

        for (int i = 0; i < answers.Length; i++)
        {
            int localR;
            int safetyCounter = 0;

            do
            {
                localR = Random.Range(minRand, maxRand);
                safetyCounter++;
                if (safetyCounter > 200)
                {
                    localR = rightAnswer + Random.Range(1, 101);
                    break;
                }
            }
            while (localR == rightAnswer || System.Array.IndexOf(answers, localR) != -1);

            answers[i] = localR;
        }
        // Place correct answer
        answers[randAnswer] = rightAnswer;

        // ---------------------------------------------------------------------
        // 1) Calculate balloon & road speeds for the current level
        float newBalloonSpeed = 1f + (currentLevel - 1) * 0.2f; 
        float newRoadSpeed    = 0.3f + (currentLevel - 1) * 0.1f ;

        // ---------------------------------------------------------------------
        // 2) Spawn balloons ONCE, assign text & speed
        for (int i = 0; i < BallonSpawnPoint.Length; i++)
        {
            Ballons[i] = Instantiate(BallonPrefab,
                                    BallonSpawnPoint[i].transform.position,
                                    Quaternion.identity);

            BalonMovement balloon = Ballons[i].GetComponent<BalonMovement>();
            balloon.newText(answers[i].ToString());
            balloon.speed = newBalloonSpeed;
        }

       
        
        if (roadMove != null)
        {
            roadMove.speed = newRoadSpeed;
        }
    }


    private int GetOperationForLevel()
    {
        switch (currentLevel)
        {
            case 1: return 0; // Only addition
            case 2: return Random.Range(0, 2); // Add/Sub
            case 3: return Random.Range(0, 2); // Add/Sub
            case 4: return Random.Range(0, 3); // Add/Sub/Mult
            case 5: return Random.Range(0, 4); // All operations
            default: return Random.Range(0, 4); // Levels 6-10: All
        }
    }
    public float emotionScore = 0f;
    public void addScore(int score)
    {

        // emotionScore = EmotionsManager.EmotionIndex;
        Debug.Log("Emotion Score: " + emotionScore);

        Score += score;
        totalQuestionCount++;
        if (score > 0)
        {
            
            difficulty += 0.034f + emotionScore * 0.01f; // Adjust difficulty based on emotion score
            timer += 10f;
            time += 10;
            rightAnswerCount++;
        }
        else
        {
            difficulty += 0.0034f + emotionScore * 0.01f;
            wrongAnswerCount++;
        }
        currentLevel = Mathf.Clamp(Mathf.FloorToInt((difficulty*10)+1), 1, 10);
        textScore.SetText(Score.ToString());
        textEmotionScore.SetText((emotionScore*100).ToString("F2"));
    }

    private void AdjustCarSpeed()
    {
        // if (carMovement != null && currentLevel >= 7)
        // {
        //     float baseSpeed = 2f;
        //     float speedIncrease = (currentLevel - 6) * 0.75f;
        //     carMovement.speed = baseSpeed + speedIncrease;
        // }
    }

    public void destroyBallons()
{
    for (int i = 0; i < Ballons.Length; i++)
    {
        if (Ballons[i] != null)
        {
            BalonMovement balloon = Ballons[i].GetComponent<BalonMovement>();
            // Only destroy balloons that haven't been hit
            if (balloon == null || !balloon.isHit)
            {
                Destroy(Ballons[i]);
            }
        }
    }
    NewQuestion();
}

    public void RestartScene()
    {
        time = defaultTime;
        textTime.SetText(time.ToString());
        Score = 0;
        textScore.SetText(Score.ToString());
        totalQuestionCount = 0;
        rightAnswerCount = 0;
        wrongAnswerCount = 0;
        difficulty = 0f;
        currentLevel = 1;
        consecutiveCorrect = 0;

        destroyBallons();
        playerCar.SetActive(true);
        menuStart.SetActive(false);
        menuGame.SetActive(true);
        menuFinish.SetActive(false);
        menuPause.SetActive(false);
        Time.timeScale = 1;
    }

    public void selectCar(int id)
    {
        selectedCar.sprite = cars[id];
        playerCar.GetComponent<SpriteRenderer>().sprite = cars[id];
    }

    public void HomeButton()
    {
        menuStart.SetActive(true);
        menuGame.SetActive(false);
        menuFinish.SetActive(false);
        menuPause.SetActive(false);
        Time.timeScale = 0;
    }

    public void MainScene()
    {
        SceneManager.LoadScene(0);
    }

    public void onValueChangedForOperations()
    {
        // Placeholder for UI compatibility
    }

    public void PauseButton()
    {
        menuStart.SetActive(false);
        menuGame.SetActive(false);
        menuFinish.SetActive(false);
        menuPause.SetActive(true);
        Time.timeScale = 0;
    }

    public void ContinueButton()
    {
        menuStart.SetActive(false);
        menuGame.SetActive(true);
        menuFinish.SetActive(false);
        menuPause.SetActive(false);
        Time.timeScale = 1;
    }

    public void RestartGameButton()
    {
        RestartScene();
        Time.timeScale = 1;
    }

    private bool isMute = false;
    public void clickMute()
    {
        if (isMute)
        {
            isMute = false;
            audioSource.Play();
            for (int i = 0; i < musicImage.Length; i++)
            {
                musicImage[i].sprite = onMusic;
            }
        }
        else
        {
            isMute = true;
            audioSource.Pause();
            for (int i = 0; i < musicImage.Length; i++)
            {
                musicImage[i].sprite = offMusic;
            }
        }
    }
}