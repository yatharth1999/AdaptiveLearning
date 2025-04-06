using UnityEngine;
using TMPro;

public class BalonMovement : MonoBehaviour
{
    public float speed;
    public TextMeshPro answerText;
    
    public int BallonAnswer = 0;
    public bool isHit = false;
    
    // Called once per frame
    void Update()
    {
        Vector2 temp = transform.position;
        temp.x += speed * Time.deltaTime;
        transform.position = temp;
    }

    public void newText(string text)
    {
        BallonAnswer = int.Parse(text);
        answerText.SetText(text);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Debug.Log("Collision detected with: " + collision.gameObject.name);
        // If the balloon collides with the car
        if (collision.gameObject.CompareTag("car"))
        {
            isHit = true;
            // Debug.Log("Balloon collided with car: " + collision.gameObject.name);
            // Get a reference to AskControl (assuming there's only one in the scene)
            AskControl askControl = FindObjectOfType<AskControl>();

            // Check if this balloon's answer matches askControl's rightAnswer
            bool isCorrect = (BallonAnswer == askControl.rightAnswer);

            if (isCorrect)
            {
                Debug.Log("Correct answer! Balloon's answer: " + BallonAnswer);
                // Color the balloon green
                GetComponent<SpriteRenderer>().color = Color.green;
                
                
            }
            else
            {
                // Color the balloon red
                GetComponent<SpriteRenderer>().color = Color.red;
                
            }

            // (Optional) Destroy balloon after a short delay
            Destroy(gameObject, 0.1f);
        }
    }
}
