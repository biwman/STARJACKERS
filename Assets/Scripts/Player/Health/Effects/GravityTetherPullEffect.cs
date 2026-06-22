using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GravityTetherPullEffect : MonoBehaviour
{
    Rigidbody2D body;
    PhotonView sourceView;
    int sourceViewId;
    Vector2 fallbackSourcePosition;
    float pullAcceleration;
    float maxSpeed;
    float expiresAt;

    public void Configure(int newSourceViewId, Vector2 sourcePosition, float acceleration, float speedLimit, float duration)
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        sourceViewId = newSourceViewId;
        fallbackSourcePosition = sourcePosition;
        pullAcceleration = Mathf.Max(0f, acceleration);
        maxSpeed = Mathf.Max(0.1f, speedLimit);
        expiresAt = Mathf.Max(expiresAt, Time.time + Mathf.Max(0.05f, duration));
        ResolveSourceView();
    }

    void FixedUpdate()
    {
        if (Time.time > expiresAt)
        {
            Destroy(this);
            return;
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
            if (body == null)
                return;
        }

        ResolveSourceView();
        Vector2 sourcePosition = sourceView != null
            ? (Vector2)sourceView.transform.position
            : fallbackSourcePosition;

        Vector2 toSource = sourcePosition - body.position;
        float distance = toSource.magnitude;
        if (distance < 0.12f)
        {
            body.linearVelocity *= 0.94f;
            return;
        }

        float ramp = Mathf.Clamp01(distance / 7.5f);
        float acceleration = pullAcceleration * Mathf.Lerp(0.72f, 1.35f, ramp);
        body.linearVelocity += toSource.normalized * acceleration * Time.fixedDeltaTime;

        if (body.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
    }

    void ResolveSourceView()
    {
        if (sourceView != null && sourceView.ViewID == sourceViewId)
            return;

        sourceView = sourceViewId > 0 ? PhotonView.Find(sourceViewId) : null;
    }
}
