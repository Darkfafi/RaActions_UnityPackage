# Changelog RaActions

## v2.0.0 - 08/09/2023
* Refactored RaActions to work with a instant execution philosophy rater than a stack processing one
* Added Events for Setting / Clearing the Root Action
* Added react functionality to the Action Processing using Dirty Marking
* Added Result & Action Success booleans to be able to determine whether or not the main execution of the method was executed as desired
* Made it so the CancelSource is now stored within the RaAction which was cancelled

## v1.0.0 - 17/02/2023
* Initial Release