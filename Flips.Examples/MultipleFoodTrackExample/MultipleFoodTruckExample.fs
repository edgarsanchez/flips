﻿module Flips.Examples.MultipleFoodTruckExample

open Flips
open Flips.Types

let solve settings =
    
    // Declare the parameters for our model
    let items = ["Hamburger"; "HotDog"; "Pizza"]
    let locations = ["Woodstock"; "Sellwood"; "Portland"]
    let profit = 
        [
            (("Woodstock", "Hamburger"), 1.50); (("Sellwood", "Hamburger"), 1.40); (("Portland", "Hamburger"), 1.90)
            (("Woodstock", "HotDog"   ), 1.20); (("Sellwood", "HotDog"   ), 1.50); (("Portland", "HotDog"   ), 1.80)
            (("Woodstock", "Pizza"    ), 2.20); (("Sellwood", "Pizza"    ), 1.70); (("Portland", "Pizza"    ), 2.00)
        ] |> Map.ofList

    let maxIngredients = Map.ofList [("Hamburger", 900.0); ("HotDog", 600.0); ("Pizza", 400.0)]
    let itemWeight = Map.ofList [("Hamburger", 0.5); ("HotDog", 0.4); ("Pizza", 0.6)]
    let maxTruckWeight = Map.ofList [("Woodstock", 200.0); ("Sellwood", 300.0); ("Portland", 280.0) ]

    // Create Decision Variable which is keyed by the tuple of Item and Location.
    // The resulting type is a Map<(string*string),Decision> 
    // to represent how much of each item we should pack for each location
    // with a Lower Bound of 0.0 and an Upper Bound of Infinity
    let numberOfItem =
        [for item in items do
            for location in locations do
                let decName = sprintf "NumberOf_%s_At_%s" item location
                let decision = Decision.createContinuous decName 0.0 infinity
                (location, item), decision]
        |> Map.ofList

    // Create the Linear Expression for the objective
    let objectiveExpression = 
        [for item in items do
            for location in locations ->
                profit.[location, item] * numberOfItem.[location, item]]
        |> List.sum            

    // Create an Objective with the name "MaximizeRevenue" the goal of Maximizing
    // the Objective Expression
    let objective = Objective.create "MaximizeRevenue" Maximize objectiveExpression
    
    // Create Total Item Maximum constraints for each item
    let maxItemConstraints =
        [for item in items do
            // The total of the Item is the sum across the Locations
            let locationSum = List.sum [for location in locations -> 1.0 * numberOfItem.[location, item]]
            let name = sprintf "MaxItemTotal_%s" item
            Constraint.create name (locationSum <== maxIngredients.[item])
        ]


    // Create a Constraint for the Max combined weight of items for each Location
    let maxWeightConstraints = 
        [for location in locations -> 
            let weightSum = List.sum [for item in items -> itemWeight.[item] * numberOfItem.[location, item]]
            let name = sprintf "MaxTotalWeight_%s" location
            Constraint.create name (weightSum <== maxTruckWeight.[location])
        ]

    // Create a Model type and pipe it through the addition of the constraints
    let model =
        Model.create objective
        |> Model.addConstraints maxItemConstraints
        |> Model.addConstraints maxWeightConstraints

    // Call the `solve` function in the Solve module to evaluate the model
    let result = Solver.solve settings model

    printfn "-- Result --"

    // Match the result of the call to solve
    // If the model could not be solved it will return a `Suboptimal` case with a message as to why
    // If the model could be solved, it will print the value of the Objective Function and the
    // values for the Decision Variables
    match result with
    | Optimal solution ->
        printfn "Objective Value: %f" (Objective.evaluate solution objective)
        
        CsvExport.exportVariablesToFile "foodtruck.party" solution.DecisionResults CsvExport.csvConfig
        
        for (decision, value) in solution.DecisionResults |> Map.toSeq do
            printfn "Decision: %A\tValue: %f" decision.Name value
    | errorCase -> 
        printfn "Unable to solve. Error: %A" errorCase