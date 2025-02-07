﻿
using UnityEngine;
using System.Collections;

namespace XClouds
{
    public class FlyCamera : MonoBehaviour
    {
        public float lookSpeed = 0.2f;
        public float moveSpeed = 15.0f;
        public float runMultiplier = 4.0f;

        private float _rotationX = 0.0f;
        private float _rotationY = 0.0f;
        private Vector3 _targetPosition;

        // Can't rely on Unity Inputs being defined,
        // so use hardcoded input handling to avoid
        // flooding errors and allow user to try demo
        // without any need for setup.
        private Vector3 _movement;
        private bool _isRunning;
        private Vector2 _mousePrev;
        private Vector2 _mouseDelta;

        void Start()
        {
            _targetPosition = transform.position;
        }

        void Update()
        {
            UpdateInput();

            _rotationX += _mouseDelta.x * Time.deltaTime * lookSpeed;
            _rotationY += _mouseDelta.y * Time.deltaTime * lookSpeed;
            _rotationY = Mathf.Clamp(_rotationY, -90, 90);

            transform.localRotation = Quaternion.AngleAxis(_rotationX, Vector3.up);
            transform.localRotation *= Quaternion.AngleAxis(_rotationY, Vector3.left);

            float run = _isRunning ? runMultiplier : 1.0f;
            _targetPosition += transform.forward * moveSpeed * run * Time.deltaTime * _movement.z;
            _targetPosition += transform.right * moveSpeed * run * Time.deltaTime * _movement.x;
            _targetPosition += transform.up * moveSpeed * run * Time.deltaTime * _movement.y;

            transform.position = Vector3.Lerp(transform.position, _targetPosition, 0.5f);
        }

        void UpdateInput()
        {
            if (Input.GetKey(KeyCode.W)) { _movement.z = 1.0f; }
            else if (Input.GetKey(KeyCode.S)) { _movement.z = -1.0f; }
            else { _movement.z = 0.0f; }

            if (Input.GetKey(KeyCode.D)) { _movement.x = 1.0f; }
            else if (Input.GetKey(KeyCode.A)) { _movement.x = -1.0f; }
            else { _movement.x = 0.0f; }

            if (Input.GetKey(KeyCode.E)) { _movement.y = 1.0f; }
            else if (Input.GetKey(KeyCode.Q)) { _movement.y = -1.0f; }
            else { _movement.y = 0.0f; }

            _isRunning = Input.GetKey(KeyCode.LeftShift);

            Vector2 mouse = (Vector2)Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                _mousePrev = mouse;
            }
            else if (Input.GetMouseButton(0))
            {
                _mouseDelta = mouse - _mousePrev;
            }
            else
            {
                _mouseDelta = Vector2.zero;
            }
        }
    }
}
