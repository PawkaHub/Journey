using UnityEngine;
using System.Collections;

public class Movement : Photon.PunBehaviour {
	//Adjust the facing angle of this character to match world co-ordinates
	public float facingAngleAdjustment = 90.0f;
	
	//Default maxSpeed
	public float maxSpeed = 15.0f;
	
	//Rigid Body to cache
	private Rigidbody cachedRigidBody = null;
	
	//Animator
	private Animator cachedAnimator = null;
	
	//Destination to reach
	private Vector3 touchPoint;
	
	//Direction we're going
	private Vector3 heading;
	
	//Should we move somewhere?
	private bool dontMove = true;
	
	/// <summary>
	/// First initialization phase
	/// </summary>
	private void Awake() {
		this.cachedRigidBody = this.GetComponent<Rigidbody>();
		this.cachedAnimator = this.GetComponent<Animator>();
	}
	
	/// <summary>
	/// Second initialization phase
	/// </summary>
	void Start () {
	}
	
	/// <summary>
	/// Fixed Update is used to ensure that networked machines are kept in sync as well as for visual state updates 
	/// </summary>
	private void FixedUpdate() {
		//Distance formulate for the x,z plane
		//sqrt(a^2 + b^2) = c (Pythagorean Theorum)
		
		//calc a^2
		float aSquared = this.transform.position.x - this.touchPoint.x;
		aSquared *= aSquared;
		
		//calc b^2
		float zSquared = this.transform.position.z - this.touchPoint.z;
		zSquared *= zSquared;
		
		//Take sqrt for distance
		float distance = Mathf.Sqrt(aSquared + zSquared);
		
		//Have we reached the threshold of our destination?
		if (distance < 0.5f) {
			//Stop moving
			this.dontMove = true;
		}
		
		//Should we move?
		if (!dontMove) {
			//Move in the heading set by the handlePointMovement function
			this.Move(this.heading.x, this.heading.z);
		} else {
			//Reset the heading to zero just in case
			this.heading = Vector3.zero;
			
			//Remove all velocity from the character 
			//except in the y axis as we don't control that, gravity does
			this.cachedRigidBody.velocity = new Vector3(0.0f, this.cachedRigidBody.velocity.y, 0.0f);
			//Stop the running animation, as we are no longer entering the move function
			this.cachedAnimator.SetBool("Run", false);
			
		}
	}
	
	/// <summary>
	/// Move the character on the x,z plane as we use y for vertical in Journey
	/// Colliders will take care of the y axis, and our character doesn't fly (for now)
	/// </summary>
	/// <param name="xMove"></param>
	/// <param name="zMove"></param>
	
	public void Move(float xMove, float zMove) {
		//Determine where we should move and set yValue to 0 so that the player doesn't jump up
		Vector3 move = new Vector3(xMove, 0, zMove);
		Debug.Log("MOVING!");
		Debug.Log(move);
		
		//Normalize this, so it has a magnitude of 1 such that we are accurately portraying our max speed
		move.Normalize();
		
		//Multiply by max speed so that we don't move super slow
		this.cachedRigidBody.velocity = move * this.maxSpeed;
		
		//Calculate our total speed for the animator
		float speed = Mathf.Abs(this.cachedRigidBody.velocity.x) + Mathf.Abs(this.cachedRigidBody.velocity.z);
		
		//Activate the running animation
		this.cachedAnimator.SetBool("Run", true);
		
		//Convert the vector into a radian angle,
		//convert to degrees and then adjust for the
		//object's starting orientation
		float angle = Mathf.Atan2(this.cachedRigidBody.velocity.z * -1, this.cachedRigidBody.velocity.x) * Mathf.Rad2Deg + facingAngleAdjustment;
		
		//Don't rotate if we don't need to
		if (speed > 0.0f) {
			//Rotate by angle around the z axis
			this.transform.rotation = Quaternion.AngleAxis(angle, new Vector3(0, 1, 0));
		}
	}

	/// <summary>
	/// This move handles network movement between players, so that it's smoother for low latency situations
	/// </summary>
	private void SyncedMovement() {
		syncTime += Time.deltaTime;
		//this.cachedRigidBody.position = Vector3.Lerp (syncStartPosition, syncEndPosition, syncTime / syncDelay);
		//this.cachedRigidBody.rotation = syncRotation;

		//Raw latency compensation - only check every 5 second/animate over 5 second?
		this.cachedRigidBody.position = Vector3.Lerp(this.cachedRigidBody.position, syncPosition, Time.deltaTime * 5);
		this.cachedRigidBody.velocity = Vector3.Lerp (this.cachedRigidBody.velocity, syncVelocity, Time.deltaTime * 5);
		this.cachedRigidBody.rotation = Quaternion.Lerp(this.cachedRigidBody.rotation, syncRotation, Time.deltaTime * 5);
		if (syncRun) {
			this.cachedAnimator.SetBool ("Run", true);
		} else {
			this.cachedAnimator.SetBool ("Run", false);
		}
	}
	
	
	/// <summary>
	/// Used for checking for information as fast as possible that does 
	/// not necessarily make impact on game state. Just check to see if
	/// we should, and then make those changes in fixed update.
	/// </summary>
	private void Update() {
		//Only let the player move their character
		if (photonView.isMine) {
			//Check for touches first
			if ((Input.touchCount > 0 && Input.GetTouch (0).phase == TouchPhase.Stationary) || (Input.touchCount > 0 && Input.GetTouch (0).phase == TouchPhase.Moved)) {
				//HandlePointMovement, and send the touch position to the screen
				HandlePointMovement (Input.GetTouch (0).position);
			} else if (Input.GetMouseButtonDown (0)) {
				//If no touch fired, fallback to see if a mouse click happened, and
				//then send the mouse position to HandlePointMovement instead
				HandlePointMovement (Input.mousePosition);
			} else {
				// If the player has lifted their finger or anything else in general has happened, stop movement.
				this.dontMove = true;
			}
		} else {
			SyncedMovement();
		}
	}
	
	/// <summary>
	/// Checks to see if there is a hit on a collider, and if there is a
	/// hit, set the new heading and save the new destination point. This
	/// allows movement to happen within a fixed update cycle.
	/// </summary>
	/// <param name="position"></param>
	private void HandlePointMovement(Vector3 position) {
		//The hit object, contains information about a hit
		RaycastHit hit;
		
		//Ray where player touched or clicked is projected in 
		//the orientation of the camera into the game world.
		//This returns a bool for if a hit happened or not, 
		//and resets hit(out) with the correct information
		Ray collisionRay = Camera.main.ScreenPointToRay(position);
		
		//NOTE: 100 is the layer to check against. For now we check against
		//all layers, but to optimize the check later we could check only
		//for touches that the player has triggered on the ground
		if (Physics.Raycast(collisionRay, out hit, 100)) {
			Debug.Log ("HIT!!!");
			//The touch point is used to determine if the character has reached the 
			//destination, so therefore we have to set it to the destination as well
			this.touchPoint = hit.point;
			
			//Vector mathematics lets us determine the direction the character needs to move in
			this.heading = this.touchPoint - this.transform.position;
			
			//Don't move is the check for whether the character should move or not, and since
			//we now want the character to move, we set this to false so they can move around.
			this.dontMove = false;
		}
	}

	/// <summary>
	/// Sync player movement over the network
	/// </summary>
	 
	// Interpolation data for smoothing out transitions and movement
	private float lastSynchronizationTime = 0.0f;
	private float syncDelay = 0.0f;
	private float syncTime = 0.0f;
	private Vector3 syncStartPosition = Vector3.zero;
	private Vector3 syncEndPosition = Vector3.zero;

	//Raw position values
	private Vector3 syncPosition = Vector3.zero;
	private Vector3 syncVelocity = Vector3.zero;
	private Quaternion syncRotation = Quaternion.identity;
	private bool syncRun = false;

	void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
		if (stream.isWriting) {
			Debug.Log ("Render our own character");
			//We own the player, send the other players our data
			stream.SendNext (this.cachedRigidBody.transform.position);
			stream.SendNext (this.cachedRigidBody.velocity);
			stream.SendNext (this.cachedRigidBody.transform.rotation);
			stream.SendNext (this.cachedAnimator.GetBool("Run"));
		} else {
			//We don't own this character, render based on received data
			Debug.Log ("Render other characters");

			//Interpolate the received values to smooth out movement
			syncPosition = (Vector3)stream.ReceiveNext();
			syncVelocity = (Vector3)stream.ReceiveNext();
			syncRotation = (Quaternion)stream.ReceiveNext();
			syncRun = (bool)stream.ReceiveNext();
			
			syncTime = 0.0f;
			syncDelay = Time.time - lastSynchronizationTime;
			lastSynchronizationTime = Time.time;

			//Calculate the sync position with velocity and delay included
			//syncStartPosition = this.cachedRigidBody.position;
			//syncEndPosition = (Vector3)stream.ReceiveNext();
			//syncEndPosition = syncPosition + syncVelocity * syncDelay;
			//syncStartPosition = this.cachedRigidBody.position;

			//syncEndPosition = syncPosition + syncVelocity * syncDelay;
			//syncStartPosition = this.cachedRigidBody.position;

			//Calculate the sync rotation with velocity and delay included
			//syncEndRotation = syncRotation + syncDelay;
			//syncStartRotation = this.cachedRigidBody.rotation;
			
			//Update the network character, with no interpolation at all
			//this.cachedRigidBody.transform.position = (Vector3)stream.ReceiveNext();
			//this.cachedRigidBody.transform.rotation = (Quaternion)stream.ReceiveNext();
			//this.cachedRigidBody.velocity = (Vector3)stream.ReceiveNext();
			//this.cachedAnimator.SetBool("Run", (bool)stream.ReceiveNext());
		}
	}

}