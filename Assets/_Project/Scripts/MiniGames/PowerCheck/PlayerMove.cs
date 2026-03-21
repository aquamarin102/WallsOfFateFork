using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Game.MiniGame.PowerCheck
{
    [RequireComponent(typeof(MiniGamePlayer))]
    public class PlayerMove : MonoBehaviour
    {
        [Header("Movement Params")]

        [SerializeField] private float _runDefaultSpeed = 6.0f;
        [SerializeField] private float _runSpeed = 6.0f;
        [SerializeField] private float _rotationSpeed = 20f;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private MiniGamePlayer _characteristics;

        private Rigidbody _rb;
        private MiniGamePlayer _playerChar;
        private bool _underDebuff;
        private GameObject _debuffEffect;

        private void Awake()
        {
            if (_cameraTransform == null)
            {
                GameObject cameraObj = GameObject.FindGameObjectWithTag("PowerCheckCamera");
                if (cameraObj != null)
                {
                    _cameraTransform = cameraObj.transform;
                }
            }

            _rb = GetComponent<Rigidbody>();

            _rb.useGravity = false;

            _characteristics = GetComponent<MiniGamePlayer>();
            // Баффы и дебаффы должны считаться от фактической стартовой скорости движения,
            // настроенной на контроллере, иначе множитель начинает работать как замедление.
            float configuredSpeed = _runSpeed > 0f ? _runSpeed : _runDefaultSpeed;
            if (configuredSpeed <= 0f && _characteristics != null)
            {
                configuredSpeed = _characteristics.Speed;
            }

            _runDefaultSpeed = configuredSpeed;
            _runSpeed = configuredSpeed;

            FindDebuffEffect();
        }

        private void FixedUpdate()
        {
            if (!_cameraTransform) _cameraTransform = GameObject.FindGameObjectWithTag("PowerCheckCamera").transform;

            if (_underDebuff && _debuffEffect != null)
            {
                _debuffEffect.SetActive(true);
            }
            else if (_debuffEffect != null)
            {
                _debuffEffect.SetActive(false);
            }
            HandleHorizontalMovement();
        }

        private void FindDebuffEffect()
        {
            // �������� ��� �������� �������
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == "Indication")
                {
                    _debuffEffect = child.gameObject;
                    _debuffEffect.SetActive(false); // ������������ ������ ��� ������
                    break; // ��������� ����� ����� ����������
                }
            }

            if (_debuffEffect == null)
            {
                Debug.LogWarning("�������� ������ � ������ 'Indication' �� ������.");
            }
        }

        public void ChangeSpeed(float speed, bool isDebuff)
        {
            if (isDebuff)
            {
                _underDebuff = !Mathf.Approximately(speed, 1f);
                _runSpeed = _runDefaultSpeed * speed;
                return;
            }

            if (!_underDebuff)
            {
                _runSpeed = _runDefaultSpeed * speed;
            }
        }

        private void HandleHorizontalMovement()
        {
            Vector2 moveInput = InputManager.GetInstance().GetMoveDirection();
            if (moveInput == null) return;
            Vector3 movePlayerInputDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            Vector3 moveDirection = Vector3.zero;

            // ��������� ���� �� ���������� ������
            float cameraAngle = _cameraTransform.eulerAngles.y * Mathf.Deg2Rad;

            // ���������� ������������������ ��������
            float sinAngle = Mathf.Sin(cameraAngle);
            float cosAngle = Mathf.Cos(cameraAngle);
            float cotAngle = cosAngle / sinAngle; // ctg(x) = cos(x) / sin(x)
            if (sinAngle != 0)
            {
                // ������� ��� moveDirection
                moveDirection.x = (movePlayerInputDirection.z + movePlayerInputDirection.x * cotAngle) / (sinAngle + cotAngle * cosAngle);
                moveDirection.z = (moveDirection.x * cosAngle - movePlayerInputDirection.x) / sinAngle;
                ////Debug.Log(moveDirection);
            }
            else
            {
                moveDirection = movePlayerInputDirection;
            }

            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
            }


            Vector3 velocity = moveDirection * _runSpeed;
            _rb.linearVelocity = new Vector3(velocity.x, _rb.linearVelocity.y, velocity.z);
        }
    }
}
