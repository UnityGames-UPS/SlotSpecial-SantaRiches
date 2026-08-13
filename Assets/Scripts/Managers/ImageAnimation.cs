using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageAnimation : MonoBehaviour
{
	internal enum ImageState
	{
		NONE,
		PLAYING,
		PAUSED
	}

	internal static ImageAnimation Instance;

	[SerializeField] internal List<Sprite> textureArray;

	[SerializeField] internal Image rendererDelegate;

	[SerializeField] internal bool useSharedMaterial = true;

	[SerializeField] internal bool doLoopAnimation = true;
	[SerializeField] private bool StartOnAwake;

	[HideInInspector]
	[SerializeField] internal ImageState currentAnimationState;

	private int indexOfTexture;

	private float idealFrameRate = 0.0416666679f;

	private float delayBetweenAnimation;

	[SerializeField] internal float AnimationSpeed = 5f;

	[SerializeField] internal float delayBetweenLoop;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		if(StartOnAwake){
			StartAnimation();
		}
	}

	private void OnEnable()
	{

	}

	private void OnDisable()
	{
		//rendererDelegate.sprite = textureArray[0];
		StopAnimation();
	}

	private void AnimationProcess()
	{
		SetTextureOfIndex();
		indexOfTexture++;
		if (indexOfTexture == textureArray.Count)
		{
			indexOfTexture = 0;
			if (doLoopAnimation)
			{
				Invoke("AnimationProcess", delayBetweenAnimation + delayBetweenLoop);
			}
		}
		else
		{
			Invoke("AnimationProcess", delayBetweenAnimation);
		}
	}

	internal void StartAnimation()
	{
		indexOfTexture = 0;
		if (currentAnimationState == ImageState.NONE)
		{
			RevertToInitialState();
			delayBetweenAnimation = idealFrameRate * (float)textureArray.Count / AnimationSpeed;
			currentAnimationState = ImageState.PLAYING;
			Invoke("AnimationProcess", delayBetweenAnimation);
		}
	}

	internal void PauseAnimation()
	{
		if (currentAnimationState == ImageState.PLAYING)
		{
			CancelInvoke("AnimationProcess");
			currentAnimationState = ImageState.PAUSED;
		}
	}

	internal void ResumeAnimation()
	{
		if (currentAnimationState == ImageState.PAUSED && !IsInvoking("AnimationProcess"))
		{
			Invoke("AnimationProcess", delayBetweenAnimation);
			currentAnimationState = ImageState.PLAYING;
		}
	}

	internal void StopAnimation()
	{
		if (currentAnimationState != 0)
		{
			rendererDelegate.sprite = textureArray[0];
			CancelInvoke("AnimationProcess");
			currentAnimationState = ImageState.NONE;
		}
	}

	internal void RevertToInitialState()
	{
		indexOfTexture = 0;
		SetTextureOfIndex();
	}

	private void SetTextureOfIndex()
	{
		if (useSharedMaterial)
		{
			rendererDelegate.sprite = textureArray[indexOfTexture];
		}
		else
		{
			rendererDelegate.sprite = textureArray[indexOfTexture];
		}
	}
}
