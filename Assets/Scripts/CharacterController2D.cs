using System;
using UnityEngine;
using UnityEngine.Events;

public class CharacterController2D : MonoBehaviour
{
	[Serializable]
	public class BoolEvent : UnityEvent<bool>
	{
	}

	[SerializeField]
	private float m_JumpForce = 400f;

	[Range(0f, 1f)]
	[SerializeField]
	private float m_CrouchSpeed = 0.36f;

	[Range(0f, 0.3f)]
	[SerializeField]
	private float m_MovementSmoothing = 0.05f;

	[SerializeField]
	private bool m_AirControl;

	[SerializeField]
	private LayerMask m_WhatIsGround;

	[SerializeField]
	private Transform m_GroundCheck;

	[SerializeField]
	private Transform m_CeilingCheck;

	[SerializeField]
	private Collider2D m_CrouchDisableCollider;

	private const float k_GroundedRadius = 0.2f;

	private bool m_Grounded;

	private const float k_CeilingRadius = 0.2f;

	private Rigidbody2D m_Rigidbody2D;

	private bool m_FacingRight = true;

	private Vector3 m_Velocity = Vector3.zero;

	[Header("Events")]
	[Space]
	public UnityEvent OnLandEvent;

	public BoolEvent OnCrouchEvent;

	private bool m_wasCrouching;

	private void Awake()
	{
		m_Rigidbody2D = GetComponent<Rigidbody2D>();
		if (OnLandEvent == null)
		{
			OnLandEvent = new UnityEvent();
		}
		if (OnCrouchEvent == null)
		{
			OnCrouchEvent = new BoolEvent();
		}
	}

	private void FixedUpdate()
	{
		bool grounded = m_Grounded;
		m_Grounded = false;
		Collider2D[] array = Physics2D.OverlapCircleAll(m_GroundCheck.position, 0.2f, m_WhatIsGround);
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].gameObject != base.gameObject)
			{
				m_Grounded = true;
				if (!grounded)
				{
					OnLandEvent.Invoke();
				}
			}
		}
	}

	public void Move(float move, bool crouch, bool jump)
	{
		if (!crouch && (bool)Physics2D.OverlapCircle(m_CeilingCheck.position, 0.2f, m_WhatIsGround))
		{
			crouch = true;
		}
		if (m_Grounded || m_AirControl)
		{
			if (crouch)
			{
				if (!m_wasCrouching)
				{
					m_wasCrouching = true;
					OnCrouchEvent.Invoke(true);
				}
				move *= m_CrouchSpeed;
				if (m_CrouchDisableCollider != null)
				{
					m_CrouchDisableCollider.enabled = false;
				}
			}
			else
			{
				if (m_CrouchDisableCollider != null)
				{
					m_CrouchDisableCollider.enabled = true;
				}
				if (m_wasCrouching)
				{
					m_wasCrouching = false;
					OnCrouchEvent.Invoke(false);
				}
			}
			Vector3 target = new Vector2(move * 10f, m_Rigidbody2D.linearVelocity.y);
			m_Rigidbody2D.linearVelocity = Vector3.SmoothDamp(m_Rigidbody2D.linearVelocity, target, ref m_Velocity, m_MovementSmoothing);
			if (move > 0f && !m_FacingRight)
			{
				Flip();
			}
			else if (move < 0f && m_FacingRight)
			{
				Flip();
			}
		}
		if (m_Grounded && jump)
		{
			m_Grounded = false;
			m_Rigidbody2D.AddForce(new Vector2(0f, m_JumpForce));
		}
	}

	private void Flip()
	{
		m_FacingRight = !m_FacingRight;
		base.transform.Rotate(0f, 180f, 0f);
	}
}
