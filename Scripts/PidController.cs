using Godot;
using System;

namespace Scripts;

public class PidController
{
    /// <summary>
    /// Clamps stored integration value to prevent wind-up in the range: [-<see cref="IntegralSaturation"/>, <see cref="IntegralSaturation"/>].
    /// </summary>
    public float IntegralSaturation;

    /// <summary>
    /// Clamps result in the range: [-<see cref="MaxResult"/>, <see cref="MaxResult"/>].
    /// </summary>
    public float MaxResult;

    private float _proportionalGain, _integralGain, _differentialGain;
    private Action<float, float> _calculate;
    private float _result;

    private bool _initialising; // First D response needs to be skipped
    private float _errorLast; // For differential
    private float _integrationStored; // For integral

    public PidController(float pGain = 0f, float iGain = 0f, float dGain = 0f, float integralSaturation = float.MaxValue, float maxResult = float.MaxValue)
    {
        SetGain(pGain, iGain, dGain);
        IntegralSaturation = integralSaturation;
        MaxResult = maxResult;
    }

    /// <summary>
    /// Set these values to tune the controller's response to error.
    /// </summary>
    /// <exception cref="NullReferenceException"></exception>
    /// <param name="pGain">Proportional gain.</param>
    /// <param name="iGain">Integral gain.</param>
    /// <param name="dGain">Differential gain.</param>
    public void SetGain(float pGain = 0f, float iGain = 0f, float dGain = 0f)
    {
        _proportionalGain = pGain;
        _integralGain = iGain;
        _differentialGain = dGain;

        _calculate = null;

        if (pGain != 0f) { _calculate += AddProportionalResponse; }   
        if (iGain != 0f) { _calculate += AddIntegralResponse; }
        if (dGain != 0f) { _calculate += AddDifferentialResponse; }

        if (_calculate == null) { throw new NullReferenceException($"At least one (gain != 0) must be assigned to utilise the {nameof(PidController)}."); }
    }

    public float GetNextValue(float dt, float current, float target) => GetNextValue(dt, target - current);
    public float GetNextValue(float dt, float error)
    {
        _result = 0f;

        if (_initialising)
        {
            _initialising = false;
            _errorLast = error;
        }
        _calculate.Invoke(dt, error);

        return Mathf.Clamp(_result, -MaxResult, MaxResult);
    }

    public void Reset()
    {
        _initialising = true;
        _integrationStored = 0f;
    }

    private void AddProportionalResponse(float _, float error) => _result += _proportionalGain * error;
    private void AddIntegralResponse(float dt, float error)
    {
        _integrationStored = Mathf.Clamp(_integrationStored + (error * dt), -IntegralSaturation, IntegralSaturation);
        _result += _integralGain * _integrationStored;
    }
    private void AddDifferentialResponse(float dt, float error)
    {
        _result += _differentialGain * ((error - _errorLast) / dt);
        _errorLast = error;
    }
}
