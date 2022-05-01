using UnityEngine;
public class GameManager : MonoBehaviour
{
    private static GameManager instance = null;
    private FadeSceneTransition fade = null;
    private GameObject player;
    private Spawner spawner;
    private CameraControl cam;

    public void Awake()
    {
        if (instance != null)
            Destroy(this.gameObject);
        else
        {
            instance = this;
            DontDestroyOnLoad(this);
        }
    }
    public static GameManager Instance { get => instance; set => instance = value; }
    public static FadeSceneTransition Fade { get => Instance.fade; set => Instance.fade = value; }
    public static GameObject Player { get => Instance.player; set => Instance.player = value; }
    public static Spawner Spawner { get => Instance.spawner; set => Instance.spawner = value; }
    public static CameraControl Cam { get => Instance.cam; set => Instance.cam = value; }
    public static void PlaySound(AudioClip clip)
    {
        GameObject sound = new GameObject("sound", typeof(AudioSource));
        sound.GetComponent<AudioSource>().PlayOneShot(clip);
    }
}