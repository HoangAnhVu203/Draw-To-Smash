using UnityEngine;

public class Swing: MonoBehaviour
{
    [Header("Swing")]
    [Tooltip("Biên độ ± (độ) tính từ hướng thẳng xuống. Ví dụ 45 = quét từ -45° đến +45°.")]
    public float maxAngle = 45f;

    [Tooltip("Vận tốc góc cố định (độ/giây). Dương quay 1 phía, script sẽ tự đảo chiều ở biên.")]
    public float speedDegPerSec = 120f;

    [Tooltip("Độ chênh gần biên thì đảo chiều (độ) để tránh giật).")]
    public float edgeEpsilon = 1.5f;

    [Header("Motor")]
    public float maxMotorTorque = 30000f; // tăng nếu dây nặng / nhiều đốt

    HingeJoint2D hj;
    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hj = GetComponent<HingeJoint2D>();

        // Cố định đầu trên bởi pivot (RB2D=Static) rồi dùng motor
        hj.useMotor = true;
        hj.useLimits = true;

        // Giới hạn góc ±maxAngle (tính theo jointAngle của hinge: 0° = hướng thiết kế ban đầu)
        var lim = hj.limits;
        lim.min = -maxAngle;
        lim.max =  maxAngle;
        hj.limits = lim;

        // Đặt motor ngay lập tức để Play là đung đưa
        ApplyMotor(Mathf.Sign(speedDegPerSec));
    }

    void FixedUpdate()
    {
        // jointAngle: góc hiện tại (độ) tương đối giữa body và connected body
        float ang = hj.jointAngle;

        // Đảo chiều khi gần chạm biên
        if (ang > (hj.limits.max - edgeEpsilon) && speedDegPerSec > 0f)
            ApplyMotor(-1f);
        else if (ang < (hj.limits.min + edgeEpsilon) && speedDegPerSec < 0f)
            ApplyMotor(+1f);

        // Giữ motor mỗi frame vật lý (ổn định khi va chạm)
        var m = hj.motor;
        m.maxMotorTorque = maxMotorTorque;
        hj.motor = m;
    }

    void ApplyMotor(float dirSign)
    {
        var m = hj.motor;
        m.motorSpeed = Mathf.Abs(speedDegPerSec) * dirSign; // độ/giây
        m.maxMotorTorque = maxMotorTorque;
        hj.motor = m;
        // cập nhật biến để biết đang quay hướng nào
        speedDegPerSec = Mathf.Abs(speedDegPerSec) * dirSign;
    }
}
